using Haley.Enums;
using Haley.Models;
using System.Threading.Tasks;
using Haley.Models;

namespace Haley.Internal {
    public interface IConsumerWorkflowDAL {
        /// <summary>
        /// Updates the existing workflow row when (consumer_id, ack_guid) is already present.
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
        Task<DbRows> ListPagedAsync(int skip, int take, DbExecutionLoad load = default);
    }
}
