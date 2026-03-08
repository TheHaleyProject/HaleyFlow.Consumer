using Haley.Abstractions;
using Haley.Enums;
using Haley.Models;
using static Haley.Internal.QueryFields;

namespace Haley.Internal {
    internal sealed class MariaConsumerOutboxDAL : MariaDALBase, IConsumerOutboxDAL {
        public MariaConsumerOutboxDAL(IDALUtilBase db) : base(db) { }

        public Task UpsertAsync(long wfId, AckOutcome outcome, DbExecutionLoad load = default)
            => Db.ExecAsync(QRY_OUTBOX.UPSERT, load,
                (WF_ID, wfId),
                (OUTCOME, (byte)outcome));

        public Task SetStatusAsync(long wfId, OutboxStatus status, string? error = null, DateTimeOffset? nextRetryAt = null, DbExecutionLoad load = default)
            => Db.ExecAsync(QRY_OUTBOX.SET_STATUS, load,
                (WF_ID, wfId),
                (STATUS, (byte)status),
                (LAST_ERROR, (object?)error ?? DBNull.Value),
                (NEXT_RETRY_AT, (object?)nextRetryAt?.UtcDateTime ?? DBNull.Value));

        public Task AddHistoryAsync(long wfId, AckOutcome outcome, OutboxStatus status, string? responsePayload, string? error, DbExecutionLoad load = default)
            => Db.ExecAsync(QRY_OUTBOX.ADD_HISTORY, load,
                (WF_ID, wfId),
                (OUTCOME, (byte)outcome),
                (STATUS, (byte)status),
                (RESPONSE_PAYLOAD, (object?)responsePayload ?? DBNull.Value),
                (ERROR, (object?)error ?? DBNull.Value));

        public Task<DbRows> ListDuePendingAsync(int take, DbExecutionLoad load = default)
            => Db.RowsAsync(QRY_OUTBOX.LIST_DUE_PENDING, load, (TAKE, take));

        public Task<DbRows> ListPagedAsync(int? status, int skip, int take, DbExecutionLoad load = default)
            => Db.RowsAsync(QRY_OUTBOX.LIST_PAGED, load,
                (STATUS, status ?? -1),
                (SKIP, skip),
                (TAKE, take));

        public Task<long> CountPendingAsync(DbExecutionLoad load = default)
            => Db.ScalarAsync<long>(QRY_OUTBOX.COUNT_PENDING, load);
    }
}
