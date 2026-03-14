using Haley.Abstractions;
using Haley.Enums;
using Haley.Models;
using Haley.Utils;
using static Haley.Internal.QueryFields;

namespace Haley.Internal {
    internal sealed class MariaConsumerInboxDAL : MariaDALBase, IConsumerInboxDAL {
        public MariaConsumerInboxDAL(IDALUtilBase db) : base(db) { }

        public Task UpsertAsync(long wfId, string? paramsJson, DbExecutionLoad load = default)
            => Db.ExecAsync(QRY_INBOX.UPSERT, load,
                (INBOX_ID, wfId),
                (PARAMS_JSON, (object?)paramsJson ?? DBNull.Value));

        public Task SetStatusAsync(long wfId, InboxStatus status, string? error = null, DbExecutionLoad load = default)
            => Db.ExecAsync(QRY_INBOX.SET_STATUS, load,
                (INBOX_ID, wfId),
                (STATUS, (byte)status),
                (LAST_ERROR, (object?)error ?? DBNull.Value));

        public Task IncrementAttemptAsync(long wfId, DbExecutionLoad load = default)
            => Db.ExecAsync(QRY_INBOX.INCREMENT_ATTEMPT, load, (INBOX_ID, wfId));

        public Task<DbRow?> GetByWfIdAsync(long wfId, DbExecutionLoad load = default)
            => Db.RowAsync(QRY_INBOX.SELECT_BY_INBOX_ID, load, (INBOX_ID, wfId));

        public Task<DbRows> ListPagedAsync(int? status, int skip, int take, DbExecutionLoad load = default)
            => Db.RowsAsync(QRY_INBOX.LIST_PAGED, load,
                (STATUS, status ?? -1),
                (SKIP, skip),
                (TAKE, take));

        public Task<long> CountPendingAsync(DbExecutionLoad load = default)
            => Db.ScalarAsync<long>(QRY_INBOX.COUNT_PENDING, load);
    }
}
