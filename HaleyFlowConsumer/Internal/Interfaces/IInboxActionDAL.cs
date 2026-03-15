using Haley.Enums;
using Haley.Models;

namespace Haley.Internal {
    /// <summary>
    /// Manages the <c>inbox_action</c> table — per-delivery checkpoint linking inbox rows to business actions.
    /// </summary>
    internal interface IInboxActionDAL {
        Task UpsertAsync(long inboxId, long actionId, InboxActionStatus status, string? error = null, DbExecutionLoad load = default);
        Task<DbRow?> GetAsync(long inboxId, long actionId, DbExecutionLoad load = default);
        Task<DbRows> GetAllByInboxAsync(long inboxId, DbExecutionLoad load = default);
    }
}
