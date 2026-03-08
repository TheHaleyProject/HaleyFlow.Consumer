using Haley.Enums;
using Haley.Models;
using System.Threading.Tasks;
using Haley.Models;

namespace Haley.Internal {
    public interface IConsumerWorkflowDAL {
        /// <summary>
        /// Inserts a new workflow row. On duplicate (consumer_id, ack_guid) returns the existing id.
        /// Returns (wfId, isNew=true) on insert, (wfId, isNew=false) on existing.
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
