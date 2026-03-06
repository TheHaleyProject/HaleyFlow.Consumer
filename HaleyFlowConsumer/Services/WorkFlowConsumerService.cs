using Haley.Abstractions;
using Haley.Enums;
using Haley.Internal;
using Haley.Models;
using Haley.Utils;
using System.Reflection;
using System.Text.Json;

namespace Haley.Services {

    /// <summary>
    /// Background consumer service. Auto-scans assemblies for <see cref="LifeCycleWrapper"/> handlers,
    /// registers with the engine on startup, dispatches events with bounded concurrency,
    /// and ACKs results via the transactional outbox.
    /// </summary>
    public sealed class WorkFlowConsumerService : IWorkFlowConsumerService {
        private readonly ILifeCycleEventFeed _feed;
        private readonly IConsumerServiceDAL _dal;
        private readonly IServiceProvider _sp;
        private readonly ConsumerServiceOptions _opt;
        private readonly WrapperRegistry _registry = new();
        private readonly SemaphoreSlim _throttle;
        private CancellationTokenSource? _cts;
        private long _consumerId;

        public WorkFlowConsumerService(ILifeCycleEventFeed feed, IConsumerServiceDAL dal, IServiceProvider sp, ConsumerServiceOptions? options = null) {
            _feed = feed ?? throw new ArgumentNullException(nameof(feed));
            _dal = dal ?? throw new ArgumentNullException(nameof(dal));
            _sp = sp ?? throw new ArgumentNullException(nameof(sp));
            _opt = options ?? new ConsumerServiceOptions();
            _throttle = new SemaphoreSlim(_opt.MaxConcurrency, _opt.MaxConcurrency); //maximum processing, to save resources and threads.
        }

        // ----------------------------------------------------------------
        // Assembly registration (setup-time, fluent)
        // ----------------------------------------------------------------

        public IWorkFlowConsumerService RegisterAssembly(Assembly assembly) {
            _registry.RegisterAssembly(assembly);
            return this;
        }

        public IWorkFlowConsumerService RegisterAssembly(string assemblyName) {
            var asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == assemblyName)
                ?? Assembly.Load(assemblyName);
            return RegisterAssembly(asm);
        }

        // ----------------------------------------------------------------
        // Lifecycle
        // ----------------------------------------------------------------

        public async Task StartAsync(CancellationToken ct = default) {
            // 1. Auto-scan all currently loaded assemblies for [LifeCycleDefinition] wrappers
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                _registry.RegisterAssembly(asm);

            // 2. Resolve discovered definition names → engine-assigned def_ids
            foreach (var name in _registry.GetPendingNames()) {
                var defId = await _feed.GetDefinitionIdAsync(_opt.EnvCode, name, ct);
                if (defId.HasValue) _registry.Resolve(name, defId.Value);
            }

            // 3. Register this consumer with the engine → get the assigned consumer ID
            _consumerId = await _feed.RegisterConsumerAsync(_opt.EnvCode, _opt.ConsumerGuid, ct);

            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            //background threads
            var token = _cts.Token;
            _ = Task.Run(() => HeartbeatLoopAsync(token), token); 
            _ = Task.Run(() => PollLoopAsync(token), token); 
            _ = Task.Run(() => OutboxLoopAsync(token), token);
        }

        public Task StopAsync(CancellationToken ct = default) {
            _cts?.Cancel();
            return Task.CompletedTask;
        }

        private async Task HeartbeatLoopAsync(CancellationToken ct) {
            while (!ct.IsCancellationRequested) {
                try {
                    await Task.Delay(_opt.HeartbeatInterval, ct);
                    await _feed.BeatConsumerAsync(_opt.EnvCode, _opt.ConsumerGuid, ct);
                } catch (OperationCanceledException) {
                    break;
                } catch {
                    // keep loop alive; next iteration will retry
                }
            }
        }

        private async Task PollLoopAsync(CancellationToken ct) {
            while (!ct.IsCancellationRequested) {
                try {
                    var transitions = await _feed.GetDueTransitionsAsync(_consumerId, _opt.AckStatus, _opt.TtlSeconds, 0, _opt.BatchSize, ct);
                    var hooks = await _feed.GetDueHooksAsync(_consumerId, _opt.AckStatus, _opt.TtlSeconds, 0, _opt.BatchSize, ct);

                    foreach (var item in transitions) await DispatchAsync(item, ct);
                    foreach (var item in hooks) await DispatchAsync(item, ct);

                    if (transitions.Count == 0 && hooks.Count == 0)
                        await Task.Delay(_opt.PollInterval, ct);
                } catch (OperationCanceledException) {
                    break;
                } catch {
                    await Task.Delay(_opt.PollInterval, ct);
                }
            }
        }

        // Acquire a throttle slot then fire-and-forget the processing task.
        // If MaxConcurrency slots are all busy, this awaits until one frees up.
        private async Task DispatchAsync(ILifeCycleDispatchItem item, CancellationToken ct) {
            await _throttle.WaitAsync(ct);
            _ = Task.Run(async () => {
                try { await ProcessItemAsync(item, ct); }
                finally { _throttle.Release(); }
            }, ct);
        }

        // ----------------------------------------------------------------
        // Dispatch one item
        // ----------------------------------------------------------------

        private async Task ProcessItemAsync(ILifeCycleDispatchItem item, CancellationToken ct) {
            var evt = item.Event;
            if (!_registry.TryGetRegistration(evt.DefinitionId, out var reg) || reg == null) return;

            // 1. Upsert workflow row (idempotent via UNIQUE(consumer_id, ack_guid))
            var load = new DbExecutionLoad(ct);
            var wfRecord = BuildWorkflowRecord(item);
            var (wfId, isNew) = await _dal.Workflow.UpsertAsync(wfRecord, load);

            // 2. Pin handler version on first event for this entity
            if (isNew) {
                var pinned = await _dal.Workflow.GetPinnedHandlerVersionAsync(evt.DefinitionId, evt.EntityId, load);
                var handlerVersion = pinned ?? (int)evt.DefinitionVersionId;
                await _dal.Workflow.SetHandlerVersionAsync(wfId, handlerVersion, _opt.DefaultHandlerUpgrade, load);
            }

            var wf = await _dal.Workflow.GetByIdAsync(wfId, load);
            if (wf == null) return;

            var effectiveVersion = _registry.ResolveHandlerVersion(evt.DefinitionId, wf.HandlerVersion ?? (int)evt.DefinitionVersionId, wf.HandlerUpgrade);

            // 3. Upsert inbox
            var payloadJson = evt.Payload != null ? JsonSerializer.Serialize(evt.Payload) : null;
            var paramsJson = evt.Params != null ? JsonSerializer.Serialize(evt.Params) : null;
            await _dal.Inbox.UpsertAsync(wfId, payloadJson, paramsJson, load);
            await _dal.Inbox.SetStatusAsync(wfId, InboxStatus.Processing, load: load);
            await _dal.Inbox.IncrementAttemptAsync(wfId, load);

            // 4. Build context
            var ctx = new ConsumerContext {
                WfId = wfId,
                ConsumerId = _consumerId,
                AckGuid = item.AckGuid,
                HandlerVersion = effectiveVersion,
                HandlerUpgrade = wf.HandlerUpgrade,
                CancellationToken = ct
            };

            // 5. Resolve wrapper from DI, inject step DAL, dispatch
            AckOutcome outcome;
            try {
                var wrapper = (LifeCycleWrapper)_sp.GetService(reg.WrapperType)
                    ?? (LifeCycleWrapper)Activator.CreateInstance(reg.WrapperType)!;
                wrapper._stepDal = _dal.InboxStep;

                outcome = item.Kind == LifeCycleEventKind.Transition
                    ? await wrapper.DispatchTransitionAsync((ILifeCycleTransitionEvent)evt, ctx)
                    : await wrapper.DispatchHookAsync((ILifeCycleHookEvent)evt, ctx);

                await _dal.Inbox.SetStatusAsync(wfId, InboxStatus.Processed, load: load);
            } catch (Exception ex) {
                outcome = AckOutcome.Retry;
                await _dal.Inbox.SetStatusAsync(wfId, InboxStatus.Failed, ex.Message, load);
            }

            // 6. Write outbox, attempt inline ACK
            await _dal.Outbox.UpsertAsync(wfId, outcome, load);
            if (string.IsNullOrWhiteSpace(item.AckGuid)) {
                // No ACK required — confirm directly without calling engine
                await _dal.Outbox.SetStatusAsync(wfId, OutboxStatus.Confirmed, load: load);
                await _dal.Outbox.AddHistoryAsync(wfId, outcome, OutboxStatus.Confirmed, null, null, load);
            } else {
                try {
                    await _feed.AckAsync(_consumerId, item.AckGuid, outcome, ct: ct);
                    await _dal.Outbox.SetStatusAsync(wfId, OutboxStatus.Confirmed, load: load);
                    await _dal.Outbox.AddHistoryAsync(wfId, outcome, OutboxStatus.Confirmed, null, null, load);
                } catch (Exception ex) {
                    await _dal.Outbox.SetStatusAsync(wfId, OutboxStatus.Pending,
                        error: ex.Message,
                        nextRetryAt: DateTimeOffset.UtcNow + _opt.OutboxRetryDelay,
                        load: load);
                }
            }
        }

        private async Task OutboxLoopAsync(CancellationToken ct) {
            while (!ct.IsCancellationRequested) {
                try {
                    var rows = await _dal.Outbox.ListDuePendingAsync(_opt.BatchSize, new DbExecutionLoad(ct));
                    foreach (var r in rows) {
                        ct.ThrowIfCancellationRequested();
                        var load = new DbExecutionLoad(ct);
                        var wfId = r.GetLong("wf_id");
                        var ackGuid = r.GetString("ack_guid") ?? string.Empty;
                        var consumerId = r.GetLong("consumer_id");
                        var outcome = (AckOutcome)r.GetByte("current_outcome");

                        if (string.IsNullOrWhiteSpace(ackGuid)) {
                            await _dal.Outbox.SetStatusAsync(wfId, OutboxStatus.Confirmed, load: load);
                            await _dal.Outbox.AddHistoryAsync(wfId, outcome, OutboxStatus.Confirmed, null, null, load);
                        } else {
                            try {
                                await _feed.AckAsync(consumerId, ackGuid, outcome, ct: ct);
                                await _dal.Outbox.SetStatusAsync(wfId, OutboxStatus.Confirmed, load: load);
                                await _dal.Outbox.AddHistoryAsync(wfId, outcome, OutboxStatus.Confirmed, null, null, load);
                            } catch (Exception ex) {
                                await _dal.Outbox.SetStatusAsync(wfId, OutboxStatus.Pending,
                                    error: ex.Message,
                                    nextRetryAt: DateTimeOffset.UtcNow + _opt.OutboxRetryDelay,
                                    load: load);
                                await _dal.Outbox.AddHistoryAsync(wfId, outcome, OutboxStatus.Failed, null, ex.Message, load);
                            }
                        }
                    }
                    if (rows.Count == 0)
                        await Task.Delay(_opt.OutboxInterval, ct);
                } catch (OperationCanceledException) {
                    break;
                } catch {
                    await Task.Delay(_opt.OutboxInterval, ct);
                }
            }
        }

        // Helpers
        private WorkflowRecord BuildWorkflowRecord(ILifeCycleDispatchItem item) {
            var evt = item.Event;
            var record = new WorkflowRecord {
                AckGuid = item.AckGuid,
                EntityId = evt.EntityId,
                Kind = item.Kind == LifeCycleEventKind.Hook ? WorkflowKind.Hook : WorkflowKind.Transition,
                ConsumerId = _consumerId,
                DefId = evt.DefinitionId,
                DefVersionId = evt.DefinitionVersionId,
                InstanceGuid = evt.InstanceGuid,
                OnSuccess = TryParseCode(evt.OnSuccessEvent),
                OnFailure = TryParseCode(evt.OnFailureEvent),
                Occurred = evt.OccurredAt.UtcDateTime
            };

            if (evt is ILifeCycleTransitionEvent te) record.EventCode = te.EventCode;
            if (evt is ILifeCycleHookEvent he) record.Route = he.Route;

            return record;
        }

        private static int? TryParseCode(string? code)
            => int.TryParse(code, out var v) ? v : null;
    }
}
