using Haley.Abstractions;
using Haley.Enums;
using Haley.Models;
using static Haley.Internal.QueryFields;
using static Haley.Internal.KeyConstants;
using Haley.Utils;

namespace Haley.Internal {
    internal sealed class MariaBusinessActionDAL : MariaDALBase, IBusinessActionDAL {
        public MariaBusinessActionDAL(IDALUtilBase db) : base(db) { }

        public async Task<long> UpsertReturnIdAsync(long instanceId, int actionCode, BusinessActionStatus status, DbExecutionLoad load = default) {
            // Fast path: action already exists for this instance.
            var existingId = await Db.ScalarAsync<long?>(QRY_BUSINESS_ACTION.SELECT_ID_BY_KEY, load,
                (INSTANCE_ID, instanceId),
                (ACTION_CODE, actionCode));
            if (existingId.HasValue && existingId.Value > 0) return existingId.Value;

            // INSERT IGNORE never burns an auto_increment slot on duplicate.
            await Db.ExecAsync(QRY_BUSINESS_ACTION.INSERT_IGNORE, load,
                (INSTANCE_ID, instanceId),
                (ACTION_CODE, actionCode),
                (STATUS, (byte)status));

            var id = await Db.ScalarAsync<long?>(QRY_BUSINESS_ACTION.SELECT_ID_BY_KEY, load,
                (INSTANCE_ID, instanceId),
                (ACTION_CODE, actionCode));
            if (id == null || id.Value <= 0) throw new InvalidOperationException("business_action upsert failed.");
            return id.Value;
        }

        public async Task<BusinessActionRecord?> GetByIdAsync(long id, DbExecutionLoad load = default) {
            var row = await Db.RowAsync(QRY_BUSINESS_ACTION.SELECT_BY_ID, load, (ID, id));
            return row == null ? null : MapRow(row);
        }

        public async Task<BusinessActionRecord?> GetByKeyAsync(long instanceId, int actionCode, DbExecutionLoad load = default) {
            var row = await Db.RowAsync(QRY_BUSINESS_ACTION.SELECT_BY_KEY, load,
                (INSTANCE_ID, instanceId),
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

        public Task SetFailedAsync(long id, string? errorMessage = null, DbExecutionLoad load = default)
            => Db.ExecAsync(QRY_BUSINESS_ACTION.SET_FAILED, load,
                (ID, id),
                (STATUS, (byte)BusinessActionStatus.Failed),
                (LAST_ERROR, (object?)errorMessage ?? DBNull.Value));

        private static BusinessActionRecord MapRow(DbRow r) => new() {
            Id         = r.GetLong(KEY_ID),
            InstanceId = r.GetLong(KEY_INSTANCE_ID),
            ActionCode = r.GetInt(KEY_ACTION_CODE),
            Status     = (BusinessActionStatus)r.GetInt(KEY_STATUS),
            StartedAt  = r.GetDateTime(KEY_STARTED_AT) ?? default,
            CompletedAt = r.GetDateTime(KEY_COMPLETED_AT),
            ResultJson = r.GetString(KEY_RESULT_JSON),
            LastError  = r.GetString(KEY_LAST_ERROR),
        };
    }
}
