using Haley.Abstractions;
using Haley.Enums;
using Haley.Models;
using Haley.Utils;
using static Haley.Internal.QueryFields;

namespace Haley.Internal {
    internal sealed class MariaInboxStatusDAL : MariaDALBase, IInboxStatusDAL {
        public MariaInboxStatusDAL(IDALUtilBase db) : base(db) { }

        public Task UpsertAsync(long wfId, string? paramsJson, DbExecutionLoad load = default)
            => Db.ExecAsync(QRY_INBOX_STATUS.UPSERT, load,
                (INBOX_ID, wfId),
                (PARAMS_JSON, (object?)paramsJson ?? DBNull.Value));

        public Task SetStatusAsync(long wfId, InboxStatus status, string? error = null, DbExecutionLoad load = default)
            => Db.ExecAsync(QRY_INBOX_STATUS.SET_STATUS, load,
                (INBOX_ID, wfId),
                (STATUS, (byte)status),
                (LAST_ERROR, (object?)error ?? DBNull.Value));

        public Task IncrementAttemptAsync(long wfId, DbExecutionLoad load = default)
            => Db.ExecAsync(QRY_INBOX_STATUS.INCREMENT_ATTEMPT, load, (INBOX_ID, wfId));

        public Task<DbRow?> GetByWfIdAsync(long wfId, DbExecutionLoad load = default)
            => Db.RowAsync(QRY_INBOX_STATUS.SELECT_BY_INBOX_ID, load, (INBOX_ID, wfId));

        public Task<DbRows> ListPagedAsync(ConsumerInboxStatusFilter filter, DbExecutionLoad load = default) {
            filter ??= new ConsumerInboxStatusFilter();
            return Db.RowsAsync(QRY_INBOX_STATUS.LIST_PAGED, load,
                (STATUS, filter.Status.HasValue ? (object?)(byte)filter.Status.Value : DBNull.Value),
                (KIND, filter.Kind.HasValue ? (object?)(byte)filter.Kind.Value : DBNull.Value),
                (INSTANCE_GUID, string.IsNullOrWhiteSpace(filter.InstanceGuid) ? DBNull.Value : filter.InstanceGuid.Trim()),
                (ACK_GUID, string.IsNullOrWhiteSpace(filter.AckGuid) ? DBNull.Value : filter.AckGuid.Trim()),
                (ROUTE, string.IsNullOrWhiteSpace(filter.Route) ? DBNull.Value : filter.Route.Trim()),
                (EVENT_CODE, filter.EventCode.HasValue ? (object?)filter.EventCode.Value : DBNull.Value),
                (SKIP, filter.Skip),
                (TAKE, filter.Take));
        }

        public Task<long> CountPendingAsync(DbExecutionLoad load = default)
            => Db.ScalarAsync<long>(QRY_INBOX_STATUS.COUNT_PENDING, load);
    }
}
