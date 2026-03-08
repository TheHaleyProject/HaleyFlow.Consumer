using Haley.Abstractions;
using Haley.Enums;
using Haley.Internal;
using static Haley.Internal.KeyConstants;
using Haley.Models;
using Haley.Utils;
using System.Reflection;
using System.Text.Json;

namespace Haley.Services {

    // WorkFlowConsumerService is the consumer-side counterpart to WorkFlowEngine.
    // It runs as a background service in the consumer process (microservice / worker).
    //
    // The big picture of how the consumer fits in:
    //   - WorkFlowEngine (host A, e.g. an API) fires events via EventRaised after each state transition.
    //   - WorkFlowConsumerService (host B, e.g. a worker process) receives those events and processes them.
    //   - "Processing" means: run the application's business logic (e.g. send an email, call a payment API)
    //     and then ACK the engine with the outcome (Processed / Retry / Dead).
    //
    // Three background loops run in parallel:
    //   HeartbeatLoop — keeps this consumer's row alive so the engine monitor doesn't skip it
    //   PollLoop      — polls the engine DB for events due for delivery and dispatches them
    //   OutboxLoop    — retries any ACKs that failed to reach the engine on the first attempt
    //
    // Handler discovery:
    //   Application code decorates wrapper classes with [LifeCycleDefinition("loan-approval")].
    //   StartAsync auto-discovers all such classes at startup, resolves their def_ids from the engine,
    //   and builds a WrapperRegistry. When an event arrives for def_id X, the registry finds the right
    //   wrapper type and the service dispatches the event to it.
    /// <summary>
    /// Background consumer service. Auto-scans assemblies for <see cref="LifeCycleWrapper"/> handlers,
    /// registers with the engine on startup, dispatches events with bounded concurrency,
    /// and ACKs results via the transactional outbox.
    /// </summary>
    public sealed class WorkFlowConsumerService : IWorkFlowConsumerService {
        private readonly ILifeCycleEngineProxy _feed;    // thin client over the engine's ACK+dispatch DB tables
        private readonly IConsumerServiceDAL _dal;     // consumer-side DB: workflow, inbox, outbox, step tables
        private readonly IServiceProvider _sp;         // DI container — used to resolve wrapper instances
        private readonly ConsumerServiceOptions _opt;
        private readonly WrapperRegistry _registry = new();  // def_id → wrapper type mapping, built at startup
        private readonly SemaphoreSlim _throttle;      // bounds concurrent event processing to MaxConcurrency
        private CancellationTokenSource? _cts;
        private long _consumerId;                      // numeric ID assigned by engine after RegisterConsumerAsync

        /// <inheritdoc/>
        public event Func<LifeCycleNotice, Task>? NoticeRaised;

        public WorkFlowConsumerService(ILifeCycleEngineProxy feed, IConsumerServiceDAL dal, IServiceProvider sp, ConsumerServiceOptions? options = null) {
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

        // Startup sequence — called once when the host starts.
        //
        //   Step 1: Scan all loaded assemblies for classes decorated with [LifeCycleDefinition].
        //           Those classes are the application's workflow handlers (e.g. LoanApprovalWrapper).
        //           They are registered by definition name into WrapperRegistry.
        //
        //   Step 2: Resolve each discovered definition name → engine-assigned def_id.
        //           The consumer knows its handlers by name ("loan-approval") but the engine uses numeric IDs
        //           internally. We call GetDefinitionIdAsync to translate. After this, the registry maps
        //           def_id → wrapper type, ready for fast O(1) lookup at dispatch time.
        //
        //   Step 3: Register this consumer process with the engine (idempotent — safe every restart).
        //           The engine assigns a numeric consumerId which we store; it's used in all ACK calls.
        //           Note: registering gives the engine a row — but it still doesn't know which definitions
        //           this consumer handles. That subscription mapping lives in Hub (ResolveConsumers callback).
        //
        //   Step 4: Start the three background loops (heartbeat, poll, outbox).
        public async Task StartAsync(CancellationToken ct = default) {
            // 1. Auto-scan all currently loaded assemblies for [LifeCycleDefinition] wrappers
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                _registry.RegisterAssembly(asm);

            // 2. Resolve discovered definition names → engine-assigned def_ids
            foreach (var name in _registry.GetPendingNames()) {
                var defId = await _feed.GetDefinitionIdAsync(_opt.EnvCode, name, ct);
                if (defId.HasValue) {
                    _registry.Resolve(name, defId.Value);
                } else {
                    FireNotice(LifeCycleNotice.Warn("REGISTRY_RESOLVE_FAILED", "REGISTRY_RESOLVE_FAILED",
                        $"Definition '{name}' not found in engine (env={_opt.EnvCode}). Handler will not receive events."));
                }
            }

            // 3. Register this consumer with the engine → get the assigned consumer ID
            _consumerId = await _feed.RegisterConsumerAsync(_opt.EnvCode, _opt.ConsumerGuid, ct);

            // Relay feed-level and engine notices through our own NoticeRaised so callers
            // only need to subscribe in one place (the consumer service).
            _feed.NoticeRaised += n => { FireNotice(n); return Task.CompletedTask; };

            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var token = _cts.Token;
            _ = Task.Run(() => HeartbeatLoopAsync(token), token);  // keeps consumer row alive
            _ = Task.Run(() => PollLoopAsync(token), token);        // fetches and dispatches due events
            _ = Task.Run(() => OutboxLoopAsync(token), token);      // retries failed ACK deliveries
        }

        public Task StopAsync(CancellationToken ct = default) {
            _cts?.Cancel();
            return Task.CompletedTask;
        }

        // Sends a heartbeat to the engine every HeartbeatInterval.
        // The engine monitor uses the last-beat timestamp to decide if this consumer is "down" —
        // if it is, the monitor postpones resending events rather than firing to a dead process.
        // If the beat call fails (network glitch), we swallow the exception and retry on the next tick.
        private async Task HeartbeatLoopAsync(CancellationToken ct) {
            while (!ct.IsCancellationRequested) {
                try {
                    await _feed.BeatConsumerAsync(_opt.EnvCode, _opt.ConsumerGuid, ct);
                    await Task.Delay(_opt.HeartbeatInterval, ct); //Beat and wait
                } catch (OperationCanceledException) {
                    break;
                } catch (Exception ex) {
                    FireNotice(LifeCycleNotice.Error("HEARTBEAT_ERROR", "HEARTBEAT_ERROR",
                        $"Consumer heartbeat failed (consumer={_opt.ConsumerGuid}): {ex.Message}", ex));
                }
            }
        }

        // Polls the engine DB for events that are due for delivery to this consumer.
        // "Due" means: ack_consumer row is Pending or Delivered AND next_due <= now AND consumer is alive.
        //
        // Two separate queries: one for lifecycle transition events, one for hook events.
        // If there's nothing due, we back off by PollInterval to avoid hammering the DB.
        // Any exception is swallowed (network blip, transient DB error) — the loop retries on the next tick.
        //
        // Note: this polling is the consumer-side pull. The engine also pushes via EventRaised at trigger time.
        // The poll catches anything the push missed (process was down, push failed, monitor resend).
        private async Task PollLoopAsync(CancellationToken ct) {
            while (!ct.IsCancellationRequested) {
                try {
                    var transitions = await _feed.GetDueTransitionsAsync(_consumerId, _opt.AckStatus, _opt.TtlSeconds, 0, _opt.BatchSize, ct);
                    var hooks = await _feed.GetDueHooksAsync(_consumerId, _opt.AckStatus, _opt.TtlSeconds, 0, _opt.BatchSize, ct);

                    foreach (var item in transitions) await DispatchItemSafeAsync(item, ct);
                    foreach (var item in hooks) await DispatchItemSafeAsync(item, ct);

                    if (transitions.Count == 0 && hooks.Count == 0)
                        await Task.Delay(_opt.PollInterval, ct);  // nothing to do — sleep before next poll
                } catch (OperationCanceledException) {
                    break;
                } catch (Exception ex) {
                    FireNotice(LifeCycleNotice.Error("POLL_ERROR", "POLL_ERROR",
                        $"PollLoop error (consumer={_opt.ConsumerGuid}): {ex.Message}", ex));
                    await Task.Delay(_opt.PollInterval, ct);  // error — back off before retrying
                }
            }
        }

        // Per-item wrapper used by the poll loop. Propagates cancellation but isolates any other
        // exception so a transient failure on one item (e.g. throttle acquisition fault) does not
        // abandon the remaining items in the batch. Items lost here will be re-sent by the engine
        // monitor after AckPendingResendAfter elapses — this wrapper minimises the abandonment window.
        private async Task DispatchItemSafeAsync(ILifeCycleDispatchItem item, CancellationToken ct) {
            ct.ThrowIfCancellationRequested();
            try {
                await DispatchAsync(item, ct);
            } catch (OperationCanceledException) {
                throw;  // propagate — poll loop exits cleanly
            } catch (Exception ex) {
                FireNotice(LifeCycleNotice.Error("DISPATCH_SCHEDULE_ERROR", "DISPATCH_SCHEDULE_ERROR",
                    $"Failed to schedule item kind={item.Kind} defId={item.Event?.DefinitionId} ackGuid={item.AckGuid}: {ex.Message}. Item will be re-sent by engine monitor.", ex));
            }
        }

        // Acquires a throttle slot then fires ProcessItemAsync as a background task.
        // The semaphore limits how many events are processed at the same time (MaxConcurrency).
        // If all slots are busy, this awaits until one frees up — applying natural back-pressure.
        // We don't await the processing task itself — that would hold up the poll loop.
        private async Task DispatchAsync(ILifeCycleDispatchItem item, CancellationToken ct) {
            await _throttle.WaitAsync(ct);
            _ = Task.Run(async () => {
                try {
                    await ProcessItemAsync(item, ct);
                } catch (Exception ex) {
                    FireNotice(LifeCycleNotice.Error("DISPATCH_ERROR", "DISPATCH_ERROR",
                        $"Unhandled exception in ProcessItemAsync kind={item.Kind} defId={item.Event?.DefinitionId} ackGuid={item.AckGuid}: {ex.Message}", ex));
                } finally {
                    _throttle.Release();
                }
            }, ct);
        }

        // ----------------------------------------------------------------
        // Dispatch one item
        // ----------------------------------------------------------------

        // Processes one event from start to finish. This is the core of the consumer service.
        //
        //   Step 1: Check the registry — do we have a wrapper for this definition? If not, ignore it.
        //           (Events for definitions this consumer doesn't handle will be dispatched to other consumers.)
        //
        //   Step 2: Upsert the workflow row — UNIQUE(consumer_id, ack_guid) makes this idempotent.
        //           If the same event is delivered twice (monitor resend), we get the existing row, not a duplicate.
        //
        //   Step 3: Pin the handler version on first delivery for this entity.
        //           Handler versioning lets the wrapper evolve (add/remove steps) without breaking in-flight
        //           workflows. The entity is pinned to the handler version active when it first arrived.
        //
        //   Step 4: Upsert the inbox row — records the params JSON for this specific delivery attempt.
        //           Mark it Processing + increment the attempt counter so we can track retries.
        //
        //   Step 5: Resolve the wrapper from DI (or activate directly), inject the step DAL, dispatch.
        //           The wrapper's DispatchTransitionAsync / DispatchHookAsync returns the AckOutcome.
        //           If the wrapper throws, we catch and set outcome=Retry (let the monitor try again).
        //
        //   Step 6: Write the outbox row with the outcome, then try to ACK the engine immediately.
        //           If the AckAsync call to the engine fails (network error, engine down), the outbox row
        //           stays Pending — the OutboxLoop will retry it until it gets through.
        private async Task ProcessItemAsync(ILifeCycleDispatchItem item, CancellationToken ct) {
            var evt = item.Event;
            if (!_registry.TryGetRegistration(evt.DefinitionId, out var reg) || reg == null) {
                FireNotice(LifeCycleNotice.Warn("REGISTRY_MISS", "REGISTRY_MISS",
                    $"No wrapper registered for defId={evt.DefinitionId} kind={item.Kind} ackGuid={item.AckGuid}. Event ignored."));
                return;
            }

            // 1. Upsert workflow row (idempotent via UNIQUE(consumer_id, ack_guid))
            var load = new DbExecutionLoad(ct);
            var wfRecord = BuildWorkflowRecord(item);
            var (wfId, isNew) = await _dal.Workflow.UpsertAsync(wfRecord, load);

            // 2. Pin handler version on first event for this entity.
            //    GetPinnedHandlerVersionAsync checks if another consumer of the same def+entity already
            //    established a version — if so, match it for consistency across consumers.
            if (isNew) {
                var pinned = await _dal.Workflow.GetPinnedHandlerVersionAsync(evt.DefinitionId, evt.EntityId, load);
                var handlerVersion = pinned ?? (int)evt.DefinitionVersionId;
                await _dal.Workflow.SetHandlerVersionAsync(wfId, handlerVersion, _opt.DefaultHandlerUpgrade, load);
            }

            var wf = await _dal.Workflow.GetByIdAsync(wfId, load);
            if (wf == null) return;

            // Resolve the effective handler version — accounts for manual upgrades (HandlerUpgrade flag).
            var effectiveVersion = _registry.ResolveHandlerVersion(evt.DefinitionId, wf.HandlerVersion ?? (int)evt.DefinitionVersionId, wf.HandlerUpgrade);

            // 3. Upsert inbox row with the params for this delivery. Each attempt overwrites the params
            //    (they should be the same on resend) and bumps the attempt counter.
            var paramsJson = evt.Params != null ? JsonSerializer.Serialize(evt.Params) : null;
            await _dal.Inbox.UpsertAsync(wfId, paramsJson, load);
            await _dal.Inbox.SetStatusAsync(wfId, InboxStatus.Processing, load: load);
            await _dal.Inbox.IncrementAttemptAsync(wfId, load);

            // 4. Build the context object passed to the wrapper — contains all the IDs the wrapper
            //    needs to call back into the consumer DAL (step recording, etc.).
            var ctx = new ConsumerContext {
                WfId = wfId,
                ConsumerId = _consumerId,
                AckGuid = item.AckGuid,
                HandlerVersion = effectiveVersion,
                HandlerUpgrade = wf.HandlerUpgrade,
                CancellationToken = ct
            };

            // 5. Resolve wrapper from DI, inject step DAL, dispatch.
            //    The wrapper contains the application's business logic. It may call external APIs,
            //    send emails, write to other DBs, etc. We don't care — we just want an AckOutcome back.
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
                // Wrapper threw — treat as transient failure. The monitor will resend after back-off.
                outcome = AckOutcome.Retry;
                await _dal.Inbox.SetStatusAsync(wfId, InboxStatus.Failed, ex.Message, load);
                FireNotice(LifeCycleNotice.Error("WRAPPER_ERROR", "WRAPPER_ERROR",
                    $"Wrapper threw during dispatch kind={item.Kind} defId={evt.DefinitionId} wfId={wfId} ackGuid={item.AckGuid}: {ex.Message}", ex));
            }

            // 6. Write outbox with the outcome, then try to ACK the engine right now (inline fast path).
            //    If the engine is unreachable, the outbox row stays Pending — OutboxLoop retries it.
            await _dal.Outbox.UpsertAsync(wfId, outcome, load);
            if (string.IsNullOrWhiteSpace(item.AckGuid)) {
                // No ACK required on this event — just mark confirmed locally, nothing to tell the engine.
                await _dal.Outbox.SetStatusAsync(wfId, OutboxStatus.Confirmed, load: load);
                await _dal.Outbox.AddHistoryAsync(wfId, outcome, OutboxStatus.Confirmed, null, null, load);
            } else {
                try {
                    await _feed.AckAsync(_consumerId, item.AckGuid, outcome, ct: ct);
                    await _dal.Outbox.SetStatusAsync(wfId, OutboxStatus.Confirmed, load: load);
                    await _dal.Outbox.AddHistoryAsync(wfId, outcome, OutboxStatus.Confirmed, null, null, load);
                } catch (Exception ex) {
                    // ACK call to engine failed — save error and next retry time. OutboxLoop will resend.
                    await _dal.Outbox.SetStatusAsync(wfId, OutboxStatus.Pending,
                        error: ex.Message,
                        nextRetryAt: DateTimeOffset.UtcNow + _opt.OutboxRetryDelay,
                        load: load);
                }
            }
        }

        // The outbox is a safety net for ACK delivery failures.
        // When ProcessItemAsync successfully completes the business logic but fails to reach the engine
        // (AckAsync throws), the outbox row stays Pending with a next_retry_at timestamp.
        // This loop periodically scans for those rows and retries the ACK call until it succeeds.
        // This ensures "at-least-once ACK delivery" — the engine will eventually know the outcome.
        private async Task OutboxLoopAsync(CancellationToken ct) {
            while (!ct.IsCancellationRequested) {
                try {
                    var rows = await _dal.Outbox.ListDuePendingAsync(_opt.BatchSize, new DbExecutionLoad(ct));
                    foreach (var r in rows) {
                        ct.ThrowIfCancellationRequested();
                        var load = new DbExecutionLoad(ct);
                        var wfId = r.GetLong(KEY_WF_ID);
                        var ackGuid = r.GetString(KEY_ACK_GUID) ?? string.Empty;
                        var consumerId = r.GetLong(KEY_CONSUMER_ID);
                        var outcome = (AckOutcome)r.GetByte(KEY_CURRENT_OUTCOME);

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
                                FireNotice(LifeCycleNotice.Error("OUTBOX_ACK_FAILED", "OUTBOX_ACK_FAILED",
                                    $"Outbox ACK retry failed wfId={wfId} ackGuid={ackGuid} outcome={outcome}: {ex.Message}", ex));
                            }
                        }
                    }
                    if (rows.Count == 0)
                        await Task.Delay(_opt.OutboxInterval, ct);
                } catch (OperationCanceledException) {
                    break;
                } catch (Exception ex) {
                    FireNotice(LifeCycleNotice.Error("OUTBOX_ERROR", "OUTBOX_ERROR",
                        $"OutboxLoop error (consumer={_opt.ConsumerGuid}): {ex.Message}", ex));
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

        // Fires a notice to all NoticeRaised subscribers. Each subscriber is invoked as an
        // independent fire-and-forget background task so that a broken subscriber cannot crash
        // the consumer loops. Errors from subscribers are swallowed (same pattern as the engine).
        private void FireNotice(LifeCycleNotice n) {
            var h = NoticeRaised;
            if (h == null) return;
            foreach (Func<LifeCycleNotice, Task> sub in h.GetInvocationList()) {
                var captured = sub;
                _ = Task.Run(async () => { try { await captured(n); } catch { } });
            }
        }
    }
}


