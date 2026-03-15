using Haley.Enums;
using Haley.Models;
using System.Threading.Tasks;

namespace Haley.Internal {
    internal interface IInboxDAL {
        /// <summary>
        /// Inserts a new inbox row or returns the existing id when ack_guid is already present.
        /// Returns (inboxId, isNew) — isNew=true when the row was just inserted (no handler version yet).
        /// </summary>
        Task<(long inboxId, bool isNew)> UpsertAsync(InboxRecord record, DbExecutionLoad load = default);
        Task<InboxRecord?> GetByIdAsync(long inboxId, DbExecutionLoad load = default);
        /// <summary>
        /// Returns the handler_version already pinned for this instance.
        /// Null if no prior inbox row for this instance has been pinned yet.
        /// </summary>
        Task<int?> GetPinnedHandlerVersionAsync(long instanceId, DbExecutionLoad load = default);
        Task SetHandlerVersionAsync(long inboxId, int handlerVersion, HandlerUpgrade upgrade, DbExecutionLoad load = default);
        Task<DbRows> ListPagedAsync(ConsumerInboxFilter filter, DbExecutionLoad load = default);
    }
}
