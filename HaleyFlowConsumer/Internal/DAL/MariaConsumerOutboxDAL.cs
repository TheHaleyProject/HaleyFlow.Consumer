using Haley.Abstractions;
using Haley.Enums;
using Haley.Models;
using static Haley.Internal.QueryFields;

namespace Haley.Internal {
    internal sealed class MariaConsumerOutboxDAL : MariaDALBase, IConsumerOutboxDAL {
        public MariaConsumerOutboxDAL(IDALUtilBase db) : base(db) { }

        public Task UpsertAsync(long wfId, AckOutcome outcome, DbExecutionLoad load = default)
            => Db.ExecAsync(QRY_OUTBOX.UPSERT, load,
                (INBOX_ID, wfId),
                (OUTCOME, (byte)outcome));

        public Task SetStatusAsync(long wfId, OutboxStatus status, string? error = null, DateTimeOffset? nextRetryAt = null, DbExecutionLoad load = default)
            => Db.ExecAsync(QRY_OUTBOX.SET_STATUS, load,
                (INBOX_ID, wfId),
                (STATUS, (byte)status),
                (LAST_ERROR, (object?)error ?? DBNull.Value),
                (NEXT_RETRY_AT, (object?)nextRetryAt?.UtcDateTime ?? DBNull.Value));

        public Task AddHistoryAsync(long wfId, AckOutcome outcome, OutboxStatus status, string? responsePayload, string? error, DbExecutionLoad load = default)
            => Db.ExecAsync(QRY_OUTBOX.ADD_HISTORY, load,
                (INBOX_ID, wfId),
                (OUTCOME, (byte)outcome),
                (STATUS, (byte)status),
                (RESPONSE_PAYLOAD, (object?)responsePayload ?? DBNull.Value),
                (ERROR, (object?)error ?? DBNull.Value));

        public Task<DbRows> ListDuePendingAsync(int take, DbExecutionLoad load = default)
            => Db.RowsAsync(QRY_OUTBOX.LIST_DUE_PENDING, load, (TAKE, take));

        public Task<DbRows> ListPagedAsync(ConsumerOutboxFilter filter, DbExecutionLoad load = default) {
            filter ??= new ConsumerOutboxFilter();
            return Db.RowsAsync(QRY_OUTBOX.LIST_PAGED, load,
                (STATUS, filter.Status.HasValue ? (object?)(byte)filter.Status.Value : DBNull.Value),
                (OUTCOME, filter.CurrentOutcome.HasValue ? (object?)(byte)filter.CurrentOutcome.Value : DBNull.Value),
                (KIND, filter.Kind.HasValue ? (object?)(byte)filter.Kind.Value : DBNull.Value),
                (DEF_ID, filter.DefId.HasValue ? (object?)filter.DefId.Value : DBNull.Value),
                (DEF_VERSION_ID, filter.DefVersionId.HasValue ? (object?)filter.DefVersionId.Value : DBNull.Value),
                (ENTITY_ID, string.IsNullOrWhiteSpace(filter.EntityId) ? DBNull.Value : filter.EntityId.Trim()),
                (INSTANCE_GUID, string.IsNullOrWhiteSpace(filter.InstanceGuid) ? DBNull.Value : filter.InstanceGuid.Trim()),
                (ACK_GUID, string.IsNullOrWhiteSpace(filter.AckGuid) ? DBNull.Value : filter.AckGuid.Trim()),
                (ROUTE, string.IsNullOrWhiteSpace(filter.Route) ? DBNull.Value : filter.Route.Trim()),
                (EVENT_CODE, filter.EventCode.HasValue ? (object?)filter.EventCode.Value : DBNull.Value),
                (SKIP, filter.Skip),
                (TAKE, filter.Take));
        }

        public Task<long> CountPendingAsync(DbExecutionLoad load = default)
            => Db.ScalarAsync<long>(QRY_OUTBOX.COUNT_PENDING, load);
    }
}
