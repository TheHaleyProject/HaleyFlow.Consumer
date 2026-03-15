using Haley.Enums;
using Haley.Models;
using System.Threading.Tasks;

namespace Haley.Internal {
    public interface IConsumerInboxDAL {
        /// <summary>
        /// Updates the existing inbox row when ack_guid is already present.
        /// Falls back to insert/upsert only when the row does not exist yet, so repeated calls
        /// do not burn auto-increment values on duplicate-key updates.
        /// </summary>
        Task<(long wfId, bool isNew)> UpsertAsync(WorkflowRecord record, DbExecutionLoad load = default);
        Task<WorkflowRecord?> GetByIdAsync(long wfId, DbExecutionLoad load = default);
        /// <summary>
        /// Returns the handler_version pinned on the earliest event for this (def_id, entity_id) pair.
        /// Null if no prior event has been pinned yet.
        /// </summary>
        Task<int?> GetPinnedHandlerVersionAsync(long defId, string entityId, DbExecutionLoad load = default);
        Task SetHandlerVersionAsync(long wfId, int handlerVersion, HandlerUpgrade upgrade, DbExecutionLoad load = default);
        Task<DbRows> ListPagedAsync(ConsumerInboxFilter filter, DbExecutionLoad load = default);
    }
}
