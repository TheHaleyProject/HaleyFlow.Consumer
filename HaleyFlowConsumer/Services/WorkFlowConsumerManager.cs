using Haley.Abstractions;
using Haley.Enums;
using Haley.Internal;
using static Haley.Internal.KeyConstants;
using Haley.Models;
using Haley.Utils;
using System.Reflection;
using System.Text.Json;

namespace Haley.Services {

    public sealed class WorkFlowConsumerManager : IWorkFlowConsumerManager {
        private readonly ILifeCycleEngineProxy _feed;
        private readonly IServiceDAL _dal;
        private readonly IServiceProvider _sp;
        private readonly WorkFlowConsumerOptions _opt;
        private readonly WrapperRegistry _registry = new();
        private readonly SemaphoreSlim _throttle;
        private CancellationTokenSource? _cts;
        private long _consumerId;

        /// <inheritdoc/>
        public long ConsumerId => _consumerId;
        /// <inheritdoc/>
        public string ConsumerGuid => _opt.ConsumerGuid;

        /// <inheritdoc/>
        public event Func<LifeCycleNotice, Task>? NoticeRaised;

        internal WorkFlowConsumerManager(ILifeCycleEngineProxy feed, IServiceDAL dal, IServiceProvider sp, WorkFlowConsumerOptions? options = null) {
            _feed = feed ?? throw new ArgumentNullException(nameof(feed));
            _dal  = dal  ?? throw new ArgumentNullException(nameof(dal));
            _sp   = sp   ?? throw new ArgumentNullException(nameof(sp));
            _opt  = options ?? new WorkFlowConsumerOptions();
            _throttle = new SemaphoreSlim(_opt.MaxConcurrency, _opt.MaxConcurrency);
        }

        // ── Assembly registration ────────────────────────────────────────────

        public IWorkFlowConsumerManager RegisterAssembly(Assembly assembly) {
            _registry.RegisterAssembly(assembly);
            return this;
        }

        public IWorkFlowConsumerManager RegisterAssembly(string assemblyName) {
            var asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == assemblyName)
                ?? Assembly.Load(assemblyName);
            return RegisterAssembly(asm);
        }

        // ── Administrative reads ─────────────────────────────────────────────

        public Task<DbRows> ListInstancesAsync(ConsumerInstanceFilter filter, CancellationToken ct = default)
            => _dal.Instance.ListPagedAsync(filter ?? new ConsumerInstanceFilter(), new DbExecutionLoad(ct));

        public Task<DbRows> ListInboxAsync(ConsumerInboxFilter filter, CancellationToken ct = default)
            => _dal.Inbox.ListPagedAsync(filter ?? new ConsumerInboxFilter(), new DbExecutionLoad(ct));

        public Task<DbRows> ListInboxStatusesAsync(ConsumerInboxStatusFilter filter, CancellationToken ct = default)
            => _dal.InboxStatus.ListPagedAsync(filter ?? new ConsumerInboxStatusFilter(), new DbExecutionLoad(ct));

        public Task<DbRows> ListOutboxAsync(ConsumerOutboxFilter filter, CancellationToken ct = default)
            => _dal.Outbox.ListPagedAsync(filter ?? new ConsumerOutboxFilter(), new DbExecutionLoad(ct));

        public Task<long> CountPendingInboxAsync(CancellationToken ct = default)
            => _dal.InboxStatus.CountPendingAsync(new DbExecutionLoad(ct));

        public Task<long> CountPendingOutboxAsync(CancellationToken ct = default)
            => _dal.Outbox.CountPendingAsync(new DbExecutionLoad(ct));

        public Task<ConsumerTimeline> GetConsumerTimelineAsync(string instanceGuid, CancellationToken ct = default)
            => _dal.Timeline.GetByInstanceGuidAsync(instanceGuid, new DbExecutionLoad(ct));

        // ── Lifecycle ────────────────────────────────────────────────────────

        public async Task StartAsync(CancellationToken ct = default) {
            var wrapperNames = _opt.WrapperAssemblies?.Where(n => !string.IsNullOrWhiteSpace(n)).ToList() ?? new List<string>();
            var hasNames = wrapperNames.Count > 0;
            var entryName = Assembly.GetEntryAssembly()?.GetName().Name;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()) {
                var asmName = asm.GetName().Name;
                var isEntry = !string.IsNullOrWhiteSpace(entryName) && string.Equals(asmName, entryName, StringComparison.OrdinalIgnoreCase);
                var matchesNames = hasNames
                    ? wrapperNames.Any(n => string.Equals(asmName, n, StringComparison.OrdinalIgnoreCase))
                    : true;

                if (!isEntry && !matchesNames) continue;
                _registry.RegisterAssembly(asm);
            }

            foreach (var name in _registry.GetPendingNames()) {
                var defId = await _feed.GetDefinitionIdAsync(_opt.EnvCode, name, ct);
                if (defId.HasValue) {
                    _registry.Resolve(name, defId.Value);
                } else {
                    FireNotice(LifeCycleNotice.Warn("REGISTRY_RESOLVE_FAILED", "REGISTRY_RESOLVE_FAILED",
                        $"Definition '{name}' not found in engine (env={_opt.EnvCode})."));
                }
            }

            ValidateWrapperActivation();

            await _feed.RegisterEnvironmentAsync(_opt.EnvCode, null, ct);
            _consumerId = await _feed.RegisterConsumerAsync(_opt.EnvCode, _opt.ConsumerGuid, ct);

            _feed.NoticeRaised += n => { FireNotice(n); return Task.CompletedTask; };

            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
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
                    await _feed.BeatConsumerAsync(_opt.EnvCode, _opt.ConsumerGuid, ct);
                    await Task.Delay(_opt.HeartbeatInterval, ct);
                } catch (OperationCanceledException) {
                    break;
                } catch (Exception ex) {
                    FireNotice(LifeCycleNotice.Error("HEARTBEAT_ERROR", "HEARTBEAT_ERROR",
                        $"Consumer heartbeat failed (consumer={_opt.ConsumerGuid}): {ex.Message}", ex));
                }
            }
        }

        private async Task PollLoopAsync(CancellationToken ct) {
            while (!ct.IsCancellationRequested) {
                try {
                    var transitions = await _feed.GetDueTransitionsAsync(_consumerId, _opt.AckStatus, _opt.TtlSeconds, 0, _opt.BatchSize, ct);
                    var hooks       = await _feed.GetDueHooksAsync(_consumerId, _opt.AckStatus, _opt.TtlSeconds, 0, _opt.BatchSize, ct);

                    foreach (var item in transitions) await DispatchItemSafeAsync(item, ct);
                    foreach (var item in hooks)       await DispatchItemSafeAsync(item, ct);

                    if (transitions.Count == 0 && hooks.Count == 0)
                        await Task.Delay(_opt.PollInterval, ct);
                } catch (OperationCanceledException) {
                    break;
                } catch (Exception ex) {
                    FireNotice(LifeCycleNotice.Error("POLL_ERROR", "POLL_ERROR",
                        $"PollLoop error (consumer={_opt.ConsumerGuid}): {ex.Message}", ex));
                    await Task.Delay(_opt.PollInterval, ct);
                }
            }
        }

        private async Task DispatchItemSafeAsync(ILifeCycleDispatchItem item, CancellationToken ct) {
            ct.ThrowIfCancellationRequested();
            try {
                await DispatchAsync(item, ct);
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception ex) {
                FireNotice(LifeCycleNotice.Error("DISPATCH_SCHEDULE_ERROR", "DISPATCH_SCHEDULE_ERROR",
                    $"Failed to schedule item kind={item.Kind} defId={item.Event?.DefinitionId} ackGuid={item.AckGuid}: {ex.Message}. Item will be re-sent by engine monitor.", ex));
            }
        }

        private async Task DispatchAsync(ILifeCycleDispatchItem item, CancellationToken ct) {
            await _throttle.WaitAsync(ct);
            var scheduled = false;
            try {
                _ = Task.Run(async () => {
                    try {
                        await ProcessItemAsync(item, ct);
                    } catch (OperationCanceledException) when (ct.IsCancellationRequested) {
                        // Shutdown path — do not surface a dispatch error notice.
                    } catch (Exception ex) {
                        FireNotice(LifeCycleNotice.Error("DISPATCH_ERROR", "DISPATCH_ERROR",
                            $"Unhandled exception in ProcessItemAsync kind={item.Kind} defId={item.Event?.DefinitionId} ackGuid={item.AckGuid}: {ex.Message}", ex));
                    } finally {
                        _throttle.Release();
                    }
                });
                scheduled = true;
            } finally {
                if (!scheduled) _throttle.Release();
            }
        }

        private void ValidateWrapperActivation() {
            foreach (var reg in _registry.GetResolvedRegistrations()) {
                if (_sp.GetService(reg.WrapperType) != null) continue;
                if (reg.WrapperType.GetConstructor(Type.EmptyTypes) != null) continue;

                var defName = string.IsNullOrWhiteSpace(reg.DefinitionName) ? $"defId={reg.DefId}" : $"definition='{reg.DefinitionName}'";
                throw new InvalidOperationException(
                    $"Wrapper activation validation failed for {reg.WrapperType.FullName} ({defName}). " +
                    "Register the wrapper type in DI, or add a parameterless constructor.");
            }
        }

        // ── Process one inbox delivery ───────────────────────────────────────

        private async Task ProcessItemAsync(ILifeCycleDispatchItem item, CancellationToken ct) {
            var evt = item.Event;
            if (item.ConsumerId != _consumerId) {
                FireNotice(LifeCycleNotice.Warn("CONSUMER_ID_MISMATCH", "CONSUMER_ID_MISMATCH",
                    $"Rejecting event: item.ConsumerId={item.ConsumerId} != this consumer ({_consumerId}/{_opt.ConsumerGuid}). ackGuid={item.AckGuid}"));
                return;
            }

            if (string.IsNullOrWhiteSpace(item.AckGuid)) {
                FireNotice(LifeCycleNotice.Error("DISPATCH_INVALID_ACK", "DISPATCH_INVALID_ACK",
                    $"Rejecting event with missing ack_guid. The consumer cannot terminally fail or cancel it on the engine without an acknowledgement key. kind={item.Kind} defId={evt.DefinitionId} entity={evt.EntityId} instance={evt.InstanceGuid}"));
                return;
            }

            if (!_registry.TryGetRegistration(evt.DefinitionId, out var reg) || reg == null) {
                var reason = $"No wrapper registered for defId={evt.DefinitionId} on consumer '{_opt.ConsumerGuid}'. Delivery rejected as terminal for this consumer.";
                await RejectDeliveryAsync(item, reason, ct);
                return;
            }

            var load = new DbExecutionLoad(ct);

            // 1. Upsert the consumer-side instance mirror (idempotent via UNIQUE(guid)).
            var instanceRecord = await EnsureInstanceMirrorAsync(
                evt.InstanceGuid ?? string.Empty,
                reg.DefinitionName,
                evt.EntityId ?? string.Empty,
                ct);
            var instanceId = instanceRecord.Id;

            // 2. Upsert inbox row (idempotent via UNIQUE(ack_guid)).
            var inboxRecord = BuildInboxRecord(item, instanceId);
            var (inboxId, isNew) = await _dal.Inbox.UpsertAsync(inboxRecord, load);

            // 3. Pin handler version on first delivery for this instance.
            if (isNew) {
                var pinned = await _dal.Inbox.GetPinnedHandlerVersionAsync(instanceId, load);
                var handlerVersion = pinned ?? instanceRecord.DefVersion;
                await _dal.Inbox.SetHandlerVersionAsync(inboxId, handlerVersion, _opt.DefaultHandlerUpgrade, load);
            }

            var inbox = await _dal.Inbox.GetByIdAsync(inboxId, load);
            if (inbox == null) return;

            var effectiveVersion = _registry.ResolveHandlerVersion(
                evt.DefinitionId,
                inbox.HandlerVersion ?? instanceRecord.DefVersion,
                inbox.HandlerUpgrade);

            // 4. Record the params and bump attempt counter.
            var paramsJson = evt.Params != null ? JsonSerializer.Serialize(evt.Params) : null;
            await _dal.InboxStatus.UpsertAsync(inboxId, paramsJson, load);
            await _dal.InboxStatus.SetStatusAsync(inboxId, InboxStatus.Processing, load: load);
            await _dal.InboxStatus.IncrementAttemptAsync(inboxId, load);

            // 5. Build context and dispatch wrapper.
            var ctx = new ConsumerContext {
                InboxId        = inboxId,
                InstanceId     = instanceId,
                InstanceGuid   = evt.InstanceGuid ?? string.Empty,
                EntityGuid     = evt.EntityId ?? string.Empty,
                AckGuid        = item.AckGuid,
                HandlerVersion = effectiveVersion,
                HandlerUpgrade = inbox.HandlerUpgrade,
                RunCount       = inboxRecord.RunCount,
                OnSuccessEvent = inboxRecord.OnSuccess,
                OnFailureEvent = inboxRecord.OnFailure,
                CancellationToken = ct
            };

            AckOutcome outcome;
            int? nextEvent = null;
            try {
                var wrapper = (_sp.GetService(reg.WrapperType) ?? Activator.CreateInstance(reg.WrapperType))
                    as LifeCycleWrapper
                    ?? throw new InvalidOperationException($"Could not activate wrapper type {reg.WrapperType.Name}.");
                wrapper._engine            = _feed;
                wrapper._businessActionDal = _dal.BusinessAction;
                wrapper._inboxActionDal    = _dal.InboxAction;

                outcome = item.Kind == LifeCycleEventKind.Transition
                    ? await wrapper.DispatchTransitionAsync((ILifeCycleTransitionEvent)evt, ctx)
                    : await wrapper.DispatchHookAsync((ILifeCycleHookEvent)evt, ctx);

                // Capture before wrapper goes out of scope — safe because each dispatch
                // activates a fresh wrapper instance, so _nextEvent belongs to this call only.
                nextEvent = wrapper._nextEvent;
                await _dal.InboxStatus.SetStatusAsync(inboxId, InboxStatus.Processed, load: load);
            } catch (Exception ex) {
                outcome = AckOutcome.Retry;
                await _dal.InboxStatus.SetStatusAsync(inboxId, InboxStatus.Failed, ex.Message, load);
                FireNotice(LifeCycleNotice.Error("WRAPPER_ERROR", "WRAPPER_ERROR",
                    $"Wrapper threw during dispatch kind={item.Kind} defId={evt.DefinitionId} inboxId={inboxId} ackGuid={item.AckGuid}: {ex.Message}", ex));
            }

            // 6. Write outbox (persists next_event for retry path) and try immediate ACK.
            await _dal.Outbox.UpsertAsync(inboxId, outcome, nextEvent, load);
            if (string.IsNullOrWhiteSpace(item.AckGuid)) {
                await _dal.Outbox.SetStatusAsync(inboxId, OutboxStatus.Confirmed, load: load);
                await _dal.Outbox.AddHistoryAsync(inboxId, outcome, OutboxStatus.Confirmed, null, null, load);
                await FireNextEventAsync(ctx.InstanceGuid, nextEvent, ct);
            } else {
                try {
                    await _feed.AckAsync(_consumerId, item.AckGuid, outcome, ct: ct);
                    await _dal.Outbox.SetStatusAsync(inboxId, OutboxStatus.Confirmed, load: load);
                    await _dal.Outbox.AddHistoryAsync(inboxId, outcome, OutboxStatus.Confirmed, null, null, load);
                    await FireNextEventAsync(ctx.InstanceGuid, nextEvent, ct);
                } catch (Exception ex) {
                    await _dal.Outbox.SetStatusAsync(inboxId, OutboxStatus.Pending,
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
                        var load    = new DbExecutionLoad(ct);
                        var inboxId = r.GetLong(KEY_INBOX_ID);
                        var ackGuid = r.GetString(KEY_ACK_GUID) ?? string.Empty;
                        var outcome = (AckOutcome)r.GetByte(KEY_CURRENT_OUTCOME);

                        var nextEvent    = r.GetNullableInt(KEY_NEXT_EVENT);
                        var instanceGuid = r.GetString(KEY_INSTANCE_GUID) ?? string.Empty;

                        if (string.IsNullOrWhiteSpace(ackGuid)) {
                            await _dal.Outbox.SetStatusAsync(inboxId, OutboxStatus.Confirmed, load: load);
                            await _dal.Outbox.AddHistoryAsync(inboxId, outcome, OutboxStatus.Confirmed, null, null, load);
                            await FireNextEventAsync(instanceGuid, nextEvent, ct);
                        } else {
                            try {
                                await _feed.AckAsync(_consumerId, ackGuid, outcome, ct: ct);
                                await _dal.Outbox.SetStatusAsync(inboxId, OutboxStatus.Confirmed, load: load);
                                await _dal.Outbox.AddHistoryAsync(inboxId, outcome, OutboxStatus.Confirmed, null, null, load);
                                await FireNextEventAsync(instanceGuid, nextEvent, ct);
                            } catch (Exception ex) {
                                await _dal.Outbox.SetStatusAsync(inboxId, OutboxStatus.Pending,
                                    error: ex.Message,
                                    nextRetryAt: DateTimeOffset.UtcNow + _opt.OutboxRetryDelay,
                                    load: load);
                                await _dal.Outbox.AddHistoryAsync(inboxId, outcome, OutboxStatus.Failed, null, ex.Message, load);
                                FireNotice(LifeCycleNotice.Error("OUTBOX_ACK_FAILED", "OUTBOX_ACK_FAILED",
                                    $"Outbox ACK retry failed inboxId={inboxId} ackGuid={ackGuid} outcome={outcome}: {ex.Message}", ex));
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

        private async Task RejectDeliveryAsync(ILifeCycleDispatchItem item, string reason, CancellationToken ct) {
            FireNotice(LifeCycleNotice.Warn("REGISTRY_MISS", "REGISTRY_MISS",
                $"{reason} ackGuid={item.AckGuid}"));

            try {
                await _feed.AckAsync(_consumerId, item.AckGuid, AckOutcome.Failed, reason, ct: ct);
            } catch (Exception ex) {
                FireNotice(LifeCycleNotice.Error("DELIVERY_REJECT_ACK_FAILED", "DELIVERY_REJECT_ACK_FAILED",
                    $"Failed to terminally reject delivery on engine. ackGuid={item.AckGuid} defId={item.Event?.DefinitionId}: {ex.Message}", ex));
            }
        }

        // ── Post-ACK next event ──────────────────────────────────────────────

        private async Task FireNextEventAsync(string instanceGuid, int? nextEvent, CancellationToken ct) {
            if (nextEvent == null) return;
            try {
                await _feed.TriggerAsync(new LifeCycleTriggerRequest {
                    InstanceGuid = instanceGuid,
                    Event        = nextEvent.Value.ToString()
                }, ct);
            } catch (Exception ex) {
                FireNotice(LifeCycleNotice.Error("NEXT_EVENT_FAILED", "NEXT_EVENT_FAILED",
                    $"AutoTransition trigger failed after ACK — instance={instanceGuid} nextEvent={nextEvent}: {ex.Message}. " +
                    $"ACK is already confirmed. Engine monitor will detect stale state.", ex));
            }
        }

        // ── Instance management ──────────────────────────────────────────────

        public async Task RecordInstanceAsync(string entityGuid, string defName, LifeCycleTriggerResult result, CancellationToken ct = default) {
            if (string.IsNullOrWhiteSpace(result.InstanceGuid)) return;
            await EnsureInstanceMirrorAsync(
                result.InstanceGuid,
                defName,
                entityGuid ?? string.Empty,
                ct);
        }

        public Task<DbRows> GetInstancesByEntityAsync(string entityGuid, CancellationToken ct = default)
            => _dal.Instance.GetByEntityAsync(entityGuid, new DbExecutionLoad(ct));

        // ── Helpers ──────────────────────────────────────────────────────────

        private InboxRecord BuildInboxRecord(ILifeCycleDispatchItem item, long instanceId) {
            var evt = item.Event;
            var record = new InboxRecord {
                AckGuid    = item.AckGuid,
                Kind       = item.Kind == LifeCycleEventKind.Hook ? WorkflowKind.Hook : WorkflowKind.Transition,
                InstanceId = instanceId,
                OnSuccess  = TryParseCode(evt.OnSuccessEvent),
                OnFailure  = TryParseCode(evt.OnFailureEvent),
                Occurred   = evt.OccurredAt.UtcDateTime
            };

            if (evt is ILifeCycleTransitionEvent te) record.EventCode = te.EventCode;
            if (evt is ILifeCycleHookEvent he) { record.Route = he.Route; record.RunCount = he.RunCount; }

            return record;
        }

        private async Task<InstanceRecord> EnsureInstanceMirrorAsync(
            string instanceGuid,
            string defName,
            string entityGuid,
            CancellationToken ct) {

            var load = new DbExecutionLoad(ct);
            var existing = await _dal.Instance.GetByGuidAsync(instanceGuid, load);
            if (existing != null) return existing;

            var mirrored = await TryFetchInstanceMirrorFromEngineAsync(instanceGuid, defName, entityGuid, ct)
                ?? throw new InvalidOperationException(
                    $"Could not mirror consumer instance from engine. instanceGuid={instanceGuid}");
            mirrored.Id = await _dal.Instance.UpsertAsync(mirrored, load);
            return mirrored;
        }

        private async Task<InstanceRecord?> TryFetchInstanceMirrorFromEngineAsync(
            string instanceGuid,
            string defNameFallback,
            string entityGuidFallback,
            CancellationToken ct) {

            try {
                var instance = await _feed.GetInstanceDataAsync(
                    new LifeCycleInstanceKey { InstanceGuid = instanceGuid },
                    ct);

                if (instance == null) {
                    FireNotice(LifeCycleNotice.Warn("INSTANCE_MIRROR_SYNC_FAILED", "INSTANCE_MIRROR_SYNC_FAILED",
                        $"Engine returned no instance data for instance={instanceGuid}."));
                    return null;
                }

                if (instance.DefinitionVersion <= 0) {
                    FireNotice(LifeCycleNotice.Warn("INSTANCE_MIRROR_SYNC_FAILED", "INSTANCE_MIRROR_SYNC_FAILED",
                        $"Engine instance data is missing the actual definition version for instance={instanceGuid}. defVersionId={instance.DefinitionVersionId}."));
                    return null;
                }

                if (instance.Created == default) {
                    FireNotice(LifeCycleNotice.Warn("INSTANCE_MIRROR_SYNC_FAILED", "INSTANCE_MIRROR_SYNC_FAILED",
                        $"Engine instance data is missing the engine-created timestamp for instance={instanceGuid}."));
                    return null;
                }

                return new InstanceRecord {
                    Guid = string.IsNullOrWhiteSpace(instance.InstanceGuid) ? instanceGuid : instance.InstanceGuid,
                    DefName = string.IsNullOrWhiteSpace(instance.DefinitionName) ? defNameFallback : instance.DefinitionName,
                    DefVersion = instance.DefinitionVersion,
                    EntityGuid = string.IsNullOrWhiteSpace(instance.EntityId) ? entityGuidFallback : instance.EntityId,
                    Created = instance.Created
                };
            } catch (Exception ex) {
                FireNotice(LifeCycleNotice.Warn("INSTANCE_MIRROR_SYNC_FAILED", "INSTANCE_MIRROR_SYNC_FAILED",
                    $"Could not fully sync consumer instance mirror from engine instance data for instance={instanceGuid}: {ex.Message}."));
                return null;
            }
        }

        private static int? TryParseCode(string? code)
            => int.TryParse(code, out var v) ? v : null;

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
