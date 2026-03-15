using Haley.Abstractions;
using Haley.Enums;
using Haley.Models;
using Haley.Utils;
using static Haley.Internal.QueryFields;
using static Haley.Internal.KeyConstants;

namespace Haley.Internal {
    internal sealed class MariaConsumerInboxDAL : MariaDALBase, IConsumerInboxDAL {
        public MariaConsumerInboxDAL(IDALUtilBase db) : base(db) { }

        public async Task<(long wfId, bool isNew)> UpsertAsync(WorkflowRecord r, DbExecutionLoad load = default) {
            // ack_guid is globally unique — no need to include consumer_id in the lookup.
            var existingId = await Db.ScalarAsync<long?>(QRY_INBOX.SELECT_ID_BY_ACK_GUID, load,
                (ACK_GUID, r.AckGuid));

            if (existingId.HasValue && existingId.Value > 0) {
                await Db.ExecAsync(QRY_INBOX.UPDATE, load,
                    (WF_ID, existingId.Value),
                    (ENTITY_ID, r.EntityId),
                    (KIND, (byte)r.Kind),
                    (DEF_ID, r.DefId),
                    (DEF_VERSION_ID, r.DefVersionId),
                    (INSTANCE_GUID, (object?)r.InstanceGuid ?? DBNull.Value),
                    (ON_SUCCESS, (object?)r.OnSuccess ?? DBNull.Value),
                    (ON_FAILURE, (object?)r.OnFailure ?? DBNull.Value),
                    (OCCURRED, r.Occurred),
                    (EVENT_CODE, (object?)r.EventCode ?? DBNull.Value),
                    (ROUTE, (object?)r.Route ?? DBNull.Value),
                    (RUN_COUNT, r.RunCount));

                return (existingId.Value, false);
            }

            // Insert path still uses UPSERT so a concurrent insert resolves safely to the existing row.
            var id = await Db.ScalarAsync<long?>(QRY_INBOX.UPSERT, load,
                (ACK_GUID, r.AckGuid),
                (ENTITY_ID, r.EntityId),
                (KIND, (byte)r.Kind),
                (CONSUMER_ID, r.ConsumerId),
                (DEF_ID, r.DefId),
                (DEF_VERSION_ID, r.DefVersionId),
                (INSTANCE_GUID, (object?)r.InstanceGuid ?? DBNull.Value),
                (ON_SUCCESS, (object?)r.OnSuccess ?? DBNull.Value),
                (ON_FAILURE, (object?)r.OnFailure ?? DBNull.Value),
                (OCCURRED, r.Occurred),
                (EVENT_CODE, (object?)r.EventCode ?? DBNull.Value),
                (ROUTE, (object?)r.Route ?? DBNull.Value),
                (RUN_COUNT, r.RunCount));

            if (id == null || id.Value <= 0) throw new InvalidOperationException("workflow upsert failed.");

            // Detect insert vs existing: a concurrent inserter may have won the race after our pre-check.
            var existing = await GetByIdAsync(id.Value, load);
            var isNew = existing?.HandlerVersion == null;
            return (id.Value, isNew);
        }

        public async Task<WorkflowRecord?> GetByIdAsync(long wfId, DbExecutionLoad load = default) {
            var row = await Db.RowAsync(QRY_INBOX.SELECT_BY_ID, load, (WF_ID, wfId));
            return row == null ? null : MapRow(row);
        }

        public async Task<int?> GetPinnedHandlerVersionAsync(long defId, string entityId, DbExecutionLoad load = default)
            => await Db.ScalarAsync<int?>(QRY_INBOX.GET_PINNED_HANDLER_VERSION, load,
                (DEF_ID, defId),
                (ENTITY_ID, entityId));

        public Task SetHandlerVersionAsync(long wfId, int handlerVersion, HandlerUpgrade upgrade, DbExecutionLoad load = default)
            => Db.ExecAsync(QRY_INBOX.SET_HANDLER_VERSION, load,
                (WF_ID, wfId),
                (HANDLER_VERSION, handlerVersion),
                (HANDLER_UPGRADE, (byte)upgrade));

        public Task<DbRows> ListPagedAsync(ConsumerInboxFilter filter, DbExecutionLoad load = default) {
            filter ??= new ConsumerInboxFilter();
            return Db.RowsAsync(QRY_INBOX.LIST_PAGED, load,
                (KIND, filter.Kind.HasValue ? (object?)(byte)filter.Kind.Value : DBNull.Value),
                (DEF_ID, filter.DefId.HasValue ? (object?)filter.DefId.Value : DBNull.Value),
                (DEF_VERSION_ID, filter.DefVersionId.HasValue ? (object?)filter.DefVersionId.Value : DBNull.Value),
                (ENTITY_ID, string.IsNullOrWhiteSpace(filter.EntityId) ? DBNull.Value : filter.EntityId.Trim()),
                (INSTANCE_GUID, string.IsNullOrWhiteSpace(filter.InstanceGuid) ? DBNull.Value : filter.InstanceGuid.Trim()),
                (ACK_GUID, string.IsNullOrWhiteSpace(filter.AckGuid) ? DBNull.Value : filter.AckGuid.Trim()),
                (ROUTE, string.IsNullOrWhiteSpace(filter.Route) ? DBNull.Value : filter.Route.Trim()),
                (EVENT_CODE, filter.EventCode.HasValue ? (object?)filter.EventCode.Value : DBNull.Value),
                (TAKE, filter.Take),
                (SKIP, filter.Skip));
        }

        private static WorkflowRecord MapRow(DbRow r) => new WorkflowRecord {
            Id = r.GetLong(KEY_ID),
            AckGuid = r.GetString(KEY_ACK_GUID) ?? string.Empty,
            EntityId = r.GetString(KEY_ENTITY_ID) ?? string.Empty,
            Kind = (WorkflowKind)r.GetByte(KEY_KIND),
            ConsumerId = r.GetLong(KEY_CONSUMER_ID),
            DefId = r.GetLong(KEY_DEF_ID),
            DefVersionId = r.GetLong(KEY_DEF_VERSION_ID),
            HandlerVersion = r.GetNullableInt(KEY_HANDLER_VERSION),
            InstanceGuid = r.GetString(KEY_INSTANCE_GUID),
            OnSuccess = r.GetNullableInt(KEY_ON_SUCCESS),
            OnFailure = r.GetNullableInt(KEY_ON_FAILURE),
            Occurred = r.GetDateTime(KEY_OCCURRED) ?? DateTime.UtcNow,
            EventCode = r.GetNullableInt(KEY_EVENT_CODE),
            Route = r.GetString(KEY_ROUTE),
            RunCount = r.GetNullableInt(KEY_RUN_COUNT) ?? 1,
            Created = r.GetDateTime(KEY_CREATED) ?? DateTime.UtcNow,
            HandlerUpgrade = (HandlerUpgrade)(r.GetNullableByte(KEY_HANDLER_UPGRADE) ?? (byte)HandlerUpgrade.Pinned)
        };
    }
}

