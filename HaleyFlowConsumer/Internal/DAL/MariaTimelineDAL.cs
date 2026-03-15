using Haley.Enums;
using Haley.Models;
using Haley.Abstractions;
using Haley.Utils;
using static Haley.Internal.KeyConstants;
using static Haley.Internal.QueryFields;

namespace Haley.Internal {
    internal sealed class MariaConsumerTimelineDAL : MariaDALBase, IConsumerTimelineDAL {
        public MariaConsumerTimelineDAL(IDALUtilBase db) : base(db) { }

        public async Task<ConsumerTimeline> GetByInstanceGuidAsync(string instanceGuid, DbExecutionLoad load = default) {
            var guid = instanceGuid?.Trim() ?? string.Empty;
            var param = (INSTANCE_GUID, (object)guid);

            var eventRows   = await Db.RowsAsync(QRY_TIMELINE.EVENTS_BY_INSTANCE,         load, param);
            var stepRows    = await Db.RowsAsync(QRY_TIMELINE.STEPS_BY_INSTANCE,          load, param);
            var historyRows = await Db.RowsAsync(QRY_TIMELINE.OUTBOX_HISTORY_BY_INSTANCE, load, param);

            // Index steps and history by inbox_id for O(1) lookup per event row.
            var stepsByInboxId   = BuildStepsLookup(stepRows);
            var historyByInboxId = BuildHistoryLookup(historyRows);

            var items = new List<ConsumerTimelineItem>(eventRows.Count);
            foreach (var r in eventRows) {
                var inboxId = r.GetLong(KEY_INBOX_ID);

                ConsumerTimelineStatus? status = null;
                if (r.GetNullableByte("inbox_status") is byte rawStatus) {
                    status = new ConsumerTimelineStatus {
                        Status       = ((InboxStatus)rawStatus).ToString(),
                        AttemptCount = r.GetNullableInt(KEY_ATTEMPT_COUNT) ?? 0,
                        LastError    = r.GetString("inbox_error"),
                        ReceivedAt   = r.GetDateTime(KEY_RECEIVED_AT) ?? DateTime.UtcNow,
                        Modified     = r.GetDateTime("inbox_modified") ?? DateTime.UtcNow,
                    };
                }

                ConsumerTimelineOutbox? outbox = null;
                if (r.GetNullableByte(KEY_CURRENT_OUTCOME) is byte rawOutcome) {
                    outbox = new ConsumerTimelineOutbox {
                        Outcome     = ((AckOutcome)rawOutcome).ToString(),
                        Status      = ((OutboxStatus)(r.GetNullableByte("outbox_status") ?? 0)).ToString(),
                        NextRetryAt = r.GetDateTime(KEY_NEXT_RETRY_AT) is DateTime nra ? new DateTimeOffset(nra, TimeSpan.Zero) : null,
                        LastError   = r.GetString("outbox_error"),
                        Modified    = r.GetDateTime("outbox_modified") ?? DateTime.UtcNow,
                        History     = historyByInboxId.TryGetValue(inboxId, out var hist) ? hist : Array.Empty<ConsumerTimelineOutboxHistory>(),
                    };
                }

                items.Add(new ConsumerTimelineItem {
                    InboxId        = inboxId,
                    AckGuid        = r.GetString(KEY_ACK_GUID) ?? string.Empty,
                    EntityId       = r.GetString(KEY_ENTITY_ID) ?? string.Empty,
                    Kind           = ((WorkflowKind)r.GetByte(KEY_KIND)).ToString(),
                    DefId          = r.GetLong(KEY_DEF_ID),
                    DefVersionId   = r.GetLong(KEY_DEF_VERSION_ID),
                    HandlerVersion = r.GetNullableInt(KEY_HANDLER_VERSION),
                    EventCode      = r.GetNullableInt(KEY_EVENT_CODE),
                    Route          = r.GetString(KEY_ROUTE),
                    RunCount       = r.GetNullableInt(KEY_RUN_COUNT) ?? 1,
                    Occurred       = r.GetDateTime(KEY_OCCURRED) ?? DateTime.UtcNow,
                    Created        = r.GetDateTime(KEY_CREATED) ?? DateTime.UtcNow,
                    InboxStatus    = status,
                    Outbox         = outbox,
                    Steps          = stepsByInboxId.TryGetValue(inboxId, out var steps) ? steps : Array.Empty<ConsumerTimelineStep>(),
                });
            }

            return new ConsumerTimeline {
                InstanceGuid = guid,
                Items        = items,
            };
        }

        private static Dictionary<long, IReadOnlyList<ConsumerTimelineStep>> BuildStepsLookup(DbRows rows) {
            var lookup = new Dictionary<long, List<ConsumerTimelineStep>>();
            foreach (var r in rows) {
                var inboxId = r.GetLong(KEY_INBOX_ID);
                if (!lookup.TryGetValue(inboxId, out var list)) {
                    list = new List<ConsumerTimelineStep>();
                    lookup[inboxId] = list;
                }
                list.Add(new ConsumerTimelineStep {
                    StepCode    = r.GetNullableInt(KEY_ACTION_CODE) ?? 0,
                    Status      = ((InboxStepStatus)(r.GetNullableByte(KEY_STATUS) ?? 0)).ToString(),
                    StartedAt   = r.GetDateTime(KEY_STARTED_AT),
                    CompletedAt = r.GetDateTime(KEY_COMPLETED_AT),
                    ResultJson  = r.GetString(KEY_RESULT_JSON),
                    LastError   = r.GetString(KEY_LAST_ERROR),
                });
            }
            return lookup.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<ConsumerTimelineStep>)kv.Value);
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
                    CreatedAt       = r.GetDateTime(KEY_CREATED_AT) ?? DateTime.UtcNow,
                });
            }
            return lookup.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<ConsumerTimelineOutboxHistory>)kv.Value);
        }
    }
}
