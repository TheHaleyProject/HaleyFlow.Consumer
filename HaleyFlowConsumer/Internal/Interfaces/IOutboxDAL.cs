using Haley.Enums;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Haley.Models;

namespace Haley.Internal {
    internal interface IOutboxDAL {
        /// <summary>
        /// Inserts an outbox row (status=Pending). On duplicate (inbox_id) resets to Pending with latest outcome.
        /// </summary>
        Task UpsertAsync(long wfId, AckOutcome outcome, DbExecutionLoad load = default);
        Task SetStatusAsync(long wfId, OutboxStatus status, string? error = null, DateTimeOffset? nextRetryAt = null, DbExecutionLoad load = default);
        Task AddHistoryAsync(long wfId, AckOutcome outcome, OutboxStatus status, string? responsePayload, string? error, DbExecutionLoad load = default);
        /// <summary>Returns pending outbox rows whose next_retry_at is due (or null), joined with inbox for ack_guid.</summary>
        Task<DbRows> ListDuePendingAsync(int take, DbExecutionLoad load = default);
        Task<DbRows> ListPagedAsync(ConsumerOutboxFilter filter, DbExecutionLoad load = default);
        Task<long> CountPendingAsync(DbExecutionLoad load = default);
    }
}
