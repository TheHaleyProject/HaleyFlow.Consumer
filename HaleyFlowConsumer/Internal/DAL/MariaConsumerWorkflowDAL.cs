using Haley.Abstractions;
using Haley.Enums;
using Haley.Models;
using Haley.Utils;
using static Haley.Internal.QueryFields;
using static Haley.Internal.KeyConstants;

namespace Haley.Internal {
    internal sealed class MariaConsumerWorkflowDAL : MariaDALBase, IConsumerWorkflowDAL {
        public MariaConsumerWorkflowDAL(IDALUtilBase db) : base(db) { }

        public async Task<(long wfId, bool isNew)> UpsertAsync(WorkflowRecord r, DbExecutionLoad load = default) {
            // LAST_INSERT_ID returns existing id on ON DUPLICATE KEY UPDATE id = LAST_INSERT_ID(id)
            var id = await Db.ScalarAsync<long?>(QRY_WORKFLOW.UPSERT, load,
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
                (ROUTE, (object?)r.Route ?? DBNull.Value));

            if (id == null || id.Value <= 0) throw new InvalidOperationException("workflow upsert failed.");

            // Detect insert vs existing: re-check if handler_version is null (new row never has it set)
            var existing = await GetByIdAsync(id.Value, load);
            var isNew = existing?.HandlerVersion == null;
            return (id.Value, isNew);
        }

        public async Task<WorkflowRecord?> GetByIdAsync(long wfId, DbExecutionLoad load = default) {
            var row = await Db.RowAsync(QRY_WORKFLOW.SELECT_BY_ID, load, (WF_ID, wfId));
            return row == null ? null : MapRow(row);
        }

        public async Task<int?> GetPinnedHandlerVersionAsync(long defId, string entityId, DbExecutionLoad load = default)
            => await Db.ScalarAsync<int?>(QRY_WORKFLOW.GET_PINNED_HANDLER_VERSION, load,
                (DEF_ID, defId),
                (ENTITY_ID, entityId));

        public Task SetHandlerVersionAsync(long wfId, int handlerVersion, HandlerUpgrade upgrade, DbExecutionLoad load = default)
            => Db.ExecAsync(QRY_WORKFLOW.SET_HANDLER_VERSION, load,
                (WF_ID, wfId),
                (HANDLER_VERSION, handlerVersion),
                (HANDLER_UPGRADE, (byte)upgrade));

        public Task<DbRows> ListPagedAsync(int skip, int take, DbExecutionLoad load = default)
            => Db.RowsAsync(QRY_WORKFLOW.LIST_PAGED, load, (TAKE, take), (SKIP, skip));

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
            Created = r.GetDateTime(KEY_CREATED) ?? DateTime.UtcNow,
            HandlerUpgrade = (HandlerUpgrade)(r.GetNullableByte(KEY_HANDLER_UPGRADE) ?? (byte)HandlerUpgrade.Pinned)
        };
    }
}


