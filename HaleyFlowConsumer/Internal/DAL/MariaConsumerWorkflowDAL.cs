using Haley.Abstractions;
using Haley.Models;
using Haley.Utils;
using static Haley.Internal.QueryFields;
using static Haley.Internal.KeyConstants;

namespace Haley.Internal {
    internal sealed class MariaConsumerWorkflowDAL : MariaDALBase, IConsumerWorkflowDAL {
        public MariaConsumerWorkflowDAL(IDALUtilBase db) : base(db) { }

        public async Task<long> UpsertAsync(EntityWorkflowRecord record, DbExecutionLoad load = default) {
            var id = await Db.ScalarAsync<long?>(QRY_WORKFLOW.UPSERT, load,
                (ENTITY, record.Entity),
                (DEF_NAME, record.DefName),
                (INSTANCE_ID, record.InstanceId),
                (IS_TRIGGERED, record.IsTriggered ? 1 : 0));

            if (id == null || id.Value <= 0)
                throw new InvalidOperationException($"Entity workflow upsert failed for entity={record.Entity} def={record.DefName}.");
            return id.Value;
        }

        public async Task<EntityWorkflowRecord?> GetByIdAsync(long id, DbExecutionLoad load = default) {
            var row = await Db.RowAsync(QRY_WORKFLOW.SELECT_BY_ID, load, (ID, id));
            return row == null ? null : MapRow(row);
        }

        public Task<DbRows> ListPagedAsync(ConsumerWorkflowFilter filter, DbExecutionLoad load = default) {
            filter ??= new ConsumerWorkflowFilter();
            return Db.RowsAsync(QRY_WORKFLOW.LIST_PAGED, load,
                (ENTITY, string.IsNullOrWhiteSpace(filter.EntityId) ? DBNull.Value : filter.EntityId.Trim()),
                (DEF_NAME, string.IsNullOrWhiteSpace(filter.DefName) ? DBNull.Value : filter.DefName.Trim()),
                (INSTANCE_ID, string.IsNullOrWhiteSpace(filter.InstanceId) ? DBNull.Value : filter.InstanceId.Trim()),
                (IS_TRIGGERED, filter.IsTriggered.HasValue ? (object?)(filter.IsTriggered.Value ? 1 : 0) : DBNull.Value),
                (TAKE, filter.Take),
                (SKIP, filter.Skip));
        }

        public Task<DbRows> GetByEntityAsync(string entityId, DbExecutionLoad load = default)
            => Db.RowsAsync(QRY_WORKFLOW.SELECT_BY_ENTITY, load, (ENTITY, entityId));

        public Task SetTriggeredAsync(string entityId, string defName, string instanceId, DbExecutionLoad load = default)
            => Db.ExecAsync(QRY_WORKFLOW.SET_TRIGGERED, load,
                (ENTITY, entityId),
                (DEF_NAME, defName),
                (INSTANCE_ID, instanceId));

        private static EntityWorkflowRecord MapRow(DbRow r) => new EntityWorkflowRecord {
            Id = r.GetLong(KEY_ID),
            Entity = r.GetString(KEY_ENTITY) ?? string.Empty,
            DefName = r.GetString(KEY_DEF_NAME) ?? string.Empty,
            InstanceId = r.GetString(KEY_INSTANCE_ID) ?? string.Empty,
            IsTriggered = (r.GetNullableByte(KEY_IS_TRIGGERED) ?? 0) == 1
        };
    }
}
