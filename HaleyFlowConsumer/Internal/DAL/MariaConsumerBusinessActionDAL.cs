using Haley.Abstractions;
using Haley.Enums;
using Haley.Models;
using static Haley.Internal.QueryFields;
using static Haley.Internal.KeyConstants;
using Haley.Utils;

namespace Haley.Internal {
    internal sealed class MariaConsumerBusinessActionDAL : MariaDALBase, IConsumerBusinessActionDAL {
        public MariaConsumerBusinessActionDAL(IDALUtilBase db) : base(db) { }

        public async Task<long> UpsertReturnIdAsync(long consumerId, long defId, string entityId, int actionCode, BusinessActionStatus status, DbExecutionLoad load = default) {
            var existingId = await Db.ScalarAsync<long?>(QRY_BUSINESS_ACTION.SELECT_ID_BY_KEY, load,
                (CONSUMER_ID, consumerId),
                (DEF_ID, defId),
                (ENTITY_ID, entityId),
                (ACTION_CODE, actionCode));
            if (existingId.HasValue && existingId.Value > 0) return existingId.Value;

            // Insert path still uses UPSERT so a concurrent inserter resolves to the same row safely.
            var id = await Db.ScalarAsync<long?>(QRY_BUSINESS_ACTION.UPSERT_RETURN_ID, load,
                (CONSUMER_ID, consumerId),
                (DEF_ID, defId),
                (ENTITY_ID, entityId),
                (ACTION_CODE, actionCode),
                (STATUS, (byte)status));
            if (id == null || id.Value <= 0) throw new InvalidOperationException("business_action upsert failed.");
            return id.Value;
        }

        public async Task<BusinessActionRecord?> GetByIdAsync(long id, DbExecutionLoad load = default) {
            var row = await Db.RowAsync(QRY_BUSINESS_ACTION.SELECT_BY_ID, load, (ID, id));
            return row == null ? null : MapRow(row);
        }

        public async Task<BusinessActionRecord?> GetByKeyAsync(long consumerId, long defId, string entityId, int actionCode, DbExecutionLoad load = default) {
            var row = await Db.RowAsync(QRY_BUSINESS_ACTION.SELECT_BY_KEY, load,
                (CONSUMER_ID, consumerId),
                (DEF_ID, defId),
                (ENTITY_ID, entityId),
                (ACTION_CODE, actionCode));
            return row == null ? null : MapRow(row);
        }

        public Task SetRunningAsync(long id, DbExecutionLoad load = default)
            => Db.ExecAsync(QRY_BUSINESS_ACTION.SET_RUNNING, load,
                (ID, id),
                (STATUS, (byte)BusinessActionStatus.Running));

        public Task SetCompletedAsync(long id, string? resultJson = null, DbExecutionLoad load = default)
            => Db.ExecAsync(QRY_BUSINESS_ACTION.SET_COMPLETED, load,
                (ID, id),
                (STATUS, (byte)BusinessActionStatus.Completed),
                (RESULT_JSON, (object?)resultJson ?? DBNull.Value));

        public Task SetFailedAsync(long id, string? resultJson = null, DbExecutionLoad load = default)
            => Db.ExecAsync(QRY_BUSINESS_ACTION.SET_FAILED, load,
                (ID, id),
                (STATUS, (byte)BusinessActionStatus.Failed),
                (RESULT_JSON, (object?)resultJson ?? DBNull.Value));

        private static BusinessActionRecord MapRow(DbRow r) => new() {
            Id = r.GetLong(KEY_ID),
            ConsumerId = r.GetLong(KEY_CONSUMER_ID),
            DefId = r.GetLong(KEY_DEF_ID),
            EntityId = r.GetString(KEY_ENTITY_ID) ?? string.Empty,
            ActionCode = r.GetInt(KEY_ACTION_CODE),
            Status = (BusinessActionStatus)r.GetInt(KEY_STATUS),
            StartedAt = r.GetDateTime(KEY_STARTED_AT) ?? DateTime.UtcNow,
            CompletedAt = r.GetDateTime(KEY_COMPLETED_AT),
            ResultJson = r.GetString(KEY_RESULT_JSON)
        };
    }
}
