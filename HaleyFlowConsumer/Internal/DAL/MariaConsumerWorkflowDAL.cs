using Haley.Abstractions;
using Haley.Enums;
using Haley.Models;
using Haley.Utils;
using static Haley.Internal.QueryFields;

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

        private static WorkflowRecord MapRow(DbRow r) => new WorkflowRecord {
            Id = r.GetLong("id"),
            AckGuid = r.GetString("ack_guid") ?? string.Empty,
            EntityId = r.GetString("entity_id") ?? string.Empty,
            Kind = (WorkflowKind)r.GetByte("kind"),
            ConsumerId = r.GetLong("consumer_id"),
            DefId = r.GetLong("def_id"),
            DefVersionId = r.GetLong("def_version_id"),
            HandlerVersion = r.GetNullableInt("handler_version"),
            InstanceGuid = r.GetString("instance_guid"),
            OnSuccess = r.GetNullableInt("on_success"),
            OnFailure = r.GetNullableInt("on_failure"),
            Occurred = r.GetDateTime("occurred") ?? DateTime.UtcNow,
            EventCode = r.GetNullableInt("event_code"),
            Route = r.GetString("route"),
            Created = r.GetDateTime("created") ?? DateTime.UtcNow,
            HandlerUpgrade = (HandlerUpgrade)(r.GetNullableByte("handler_upgrade") ?? (byte)HandlerUpgrade.Pinned)
        };
    }
}
