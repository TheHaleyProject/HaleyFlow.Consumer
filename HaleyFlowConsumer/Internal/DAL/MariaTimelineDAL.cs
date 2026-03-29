using Haley.Enums;
using Haley.Models;
using Haley.Abstractions;
using Haley.Utils;
using static Haley.Internal.KeyConstants;
using static Haley.Internal.QueryFields;

namespace Haley.Internal {
    internal sealed class MariaTimelineDAL : MariaDALBase, ITimelineDAL {
        public MariaTimelineDAL(IDALUtilBase db) : base(db) { }

        public async Task<ConsumerTimeline> GetByInstanceGuidAsync(string instanceGuid, DbExecutionLoad load = default) {
            var guid = instanceGuid?.Trim() ?? string.Empty;
            var param = (INSTANCE_GUID, (object)guid);

            var instanceRow = await Db.RowAsync(QRY_TIMELINE.INSTANCE_BY_GUID, load, param);
            var eventRows   = await Db.RowsAsync(QRY_TIMELINE.EVENTS_BY_INSTANCE,         load, param);
            var actionRows  = await Db.RowsAsync(QRY_TIMELINE.ACTIONS_BY_INSTANCE,        load, param);
            var historyRows = await Db.RowsAsync(QRY_TIMELINE.OUTBOX_HISTORY_BY_INSTANCE, load, param);

            var actionsByInboxId = BuildActionsLookup(actionRows);
            var historyByInboxId = BuildHistoryLookup(historyRows);

            var instanceId = instanceRow?.GetLong(KEY_ID) ?? 0;
            var entityGuid = instanceRow?.GetString(KEY_ENTITY_GUID) ?? string.Empty;
            var defName    = instanceRow?.GetString(KEY_DEF_NAME) ?? string.Empty;
            var defVersion = instanceRow?.GetNullableInt(KEY_DEF_VERSION) ?? 0;
            var created    = instanceRow?.GetDateTime(KEY_CREATED) ?? default;

            var items = new List<ConsumerTimelineItem>(eventRows.Count);
            foreach (var r in eventRows) {
                var inboxId = r.GetLong(KEY_INBOX_ID);
                var kind = (WorkflowKind)r.GetByte(KEY_KIND);

                ConsumerTimelineStatus? status = null;
                if (r.GetNullableByte("inbox_status") is byte rawStatus) {
                    status = new ConsumerTimelineStatus {
                        Status       = ((InboxStatus)rawStatus).ToString(),
                        AttemptCount = r.GetNullableInt(KEY_ATTEMPT_COUNT) ?? 0,
                        LastError    = r.GetString("inbox_error"),
                        ReceivedAt   = r.GetDateTime(KEY_RECEIVED_AT) ?? r.GetDateTime(KEY_CREATED) ?? default,
                        Modified     = r.GetDateTime("inbox_modified") ?? r.GetDateTime(KEY_RECEIVED_AT) ?? default,
                    };
                }

                ConsumerTimelineOutbox? outbox = null;
                if (r.GetNullableByte(KEY_CURRENT_OUTCOME) is byte rawOutcome) {
                    outbox = new ConsumerTimelineOutbox {
                        Outcome     = ((AckOutcome)rawOutcome).ToString(),
                        Status      = ((OutboxStatus)(r.GetNullableByte("outbox_status") ?? 0)).ToString(),
                        NextRetryAt = r.GetDateTime(KEY_NEXT_RETRY_AT) is DateTime nra ? new DateTimeOffset(nra, TimeSpan.Zero) : null,
                        LastError   = r.GetString("outbox_error"),
                        Modified    = r.GetDateTime("outbox_modified") ?? default,
                        History     = historyByInboxId.TryGetValue(inboxId, out var hist) ? hist : Array.Empty<ConsumerTimelineOutboxHistory>(),
                    };
                }

                var rawHookType = r.GetNullableByte(KEY_HOOK_TYPE);
                items.Add(new ConsumerTimelineItem {
                    InboxId        = inboxId,
                    AckGuid        = r.GetString(KEY_ACK_GUID) ?? string.Empty,
                    Kind           = kind.ToString(),
                    HandlerVersion = r.GetNullableInt(KEY_HANDLER_VERSION),
                    EventCode      = r.GetNullableInt(KEY_EVENT_CODE),
                    Route          = r.GetString(KEY_ROUTE),
                    DispatchMode   = kind == WorkflowKind.Transition
                        ? (TransitionDispatchMode?)(r.GetNullableByte(KEY_DISPATCH_MODE) ?? 0)
                        : null,
                    HookType       = rawHookType.HasValue ? (HookType?)rawHookType.Value : null,
                    NextEvent      = r.GetNullableInt(KEY_NEXT_EVENT),
                    NextEventSource = FormatNextEventSource(r.GetNullableByte(KEY_NEXT_EVENT_SOURCE)),
                    RunCount       = r.GetNullableInt(KEY_RUN_COUNT) ?? 1,
                    Occurred       = r.GetDateTime(KEY_OCCURRED) ?? r.GetDateTime(KEY_CREATED) ?? default,
                    Created        = r.GetDateTime(KEY_CREATED) ?? r.GetDateTime(KEY_OCCURRED) ?? default,
                    InboxStatus    = status,
                    Outbox         = outbox,
                    Actions        = actionsByInboxId.TryGetValue(inboxId, out var acts) ? acts : Array.Empty<ConsumerTimelineAction>(),
                });
            }

            return new ConsumerTimeline {
                InstanceId   = instanceId,
                InstanceGuid = guid,
                EntityGuid   = entityGuid,
                DefName      = defName,
                DefVersion   = defVersion,
                Created      = created,
                Instance     = instanceRow == null ? null : new ConsumerTimelineInstance {
                    Id = instanceId,
                    Guid = guid,
                    EntityGuid = entityGuid,
                    DefName = defName,
                    DefVersion = defVersion,
                    Created = created
                },
                Items        = items,
            };
        }

        private static Dictionary<long, IReadOnlyList<ConsumerTimelineAction>> BuildActionsLookup(DbRows rows) {
            var lookup = new Dictionary<long, List<ConsumerTimelineAction>>();
            foreach (var r in rows) {
                var inboxId = r.GetLong(KEY_INBOX_ID);
                if (!lookup.TryGetValue(inboxId, out var list)) {
                    list = new List<ConsumerTimelineAction>();
                    lookup[inboxId] = list;
                }
                list.Add(new ConsumerTimelineAction {
                    ActionId       = r.GetLong(KEY_ACTION_ID),
                    ActionCode     = r.GetNullableInt(KEY_ACTION_CODE) ?? 0,
                    DeliveryStatus = ((InboxActionStatus)(r.GetNullableByte("inbox_action_status") ?? 0)).ToString(),
                    DeliveryError  = r.GetString("inbox_action_error"),
                    BusinessStatus = ((BusinessActionStatus)(r.GetNullableByte("business_status") ?? 0)).ToString(),
                    StartedAt      = r.GetDateTime(KEY_STARTED_AT),
                    CompletedAt    = r.GetDateTime(KEY_COMPLETED_AT),
                    ResultJson     = r.GetString(KEY_RESULT_JSON),
                });
            }
            return lookup.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<ConsumerTimelineAction>)kv.Value);
        }

        private static Dictionary<long, IReadOnlyList<ConsumerTimelineOutboxHistory>> BuildHistoryLookup(DbRows rows) {
            var lookup = new Dictionary<long, List<ConsumerTimelineOutboxHistory>>();
            foreach (var r in rows) {
                var inboxId = r.GetLong(KEY_INBOX_ID);
                if (!lookup.TryGetValue(inboxId, out var list)) {
                    list = new List<ConsumerTimelineOutboxHistory>();
                    lookup[inboxId] = list;
                }
                list.Add(new ConsumerTimelineOutboxHistory {
                    AttemptNo       = r.GetNullableInt(KEY_ATTEMPT_NO) ?? 0,
                    Outcome         = ((AckOutcome)(r.GetNullableByte(KEY_OUTCOME) ?? 0)).ToString(),
                    Status          = ((OutboxStatus)(r.GetNullableByte(KEY_STATUS) ?? 0)).ToString(),
                    ResponsePayload = r.GetString(KEY_RESPONSE_PAYLOAD),
                    Error           = r.GetString(KEY_ERROR),
                    CreatedAt       = r.GetDateTime(KEY_CREATED_AT) ?? default,
                });
            }
            return lookup.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<ConsumerTimelineOutboxHistory>)kv.Value);
        }

        private static string? FormatNextEventSource(byte? raw)
            => raw switch {
                (byte)NextEventSource.Policy => "Policy",
                (byte)NextEventSource.EngineResolved => "EngineResolved",
                (byte)NextEventSource.ConsumerOverride => "ConsumerOverride",
                _ => null
            };
    }
}
