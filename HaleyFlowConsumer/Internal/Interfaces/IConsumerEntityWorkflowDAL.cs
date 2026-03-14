using Haley.Models;

namespace Haley.Internal {
    /// <summary>
    /// Manages the <c>workflow</c> table — tracks which workflow definitions an entity is enrolled in
    /// and whether the engine trigger has been called.
    /// </summary>
    public interface IConsumerEntityWorkflowDAL {
        /// <summary>
        /// Inserts or updates an entity-workflow mapping row.
        /// Returns the surrogate row ID.
        /// </summary>
        Task<long> UpsertAsync(EntityWorkflowRecord record, DbExecutionLoad load = default);

        Task<EntityWorkflowRecord?> GetByIdAsync(long id, DbExecutionLoad load = default);

        /// <summary>Returns all workflow rows for the given entity, ordered newest first.</summary>
        Task<DbRows> GetByEntityAsync(string entityId, DbExecutionLoad load = default);

        /// <summary>
        /// After a successful engine trigger, stamps the instance GUID and marks is_triggered = true.
        /// Matches on (entity, def_name) so the correct row is updated regardless of ID.
        /// </summary>
        Task SetTriggeredAsync(string entityId, string defName, string instanceId, DbExecutionLoad load = default);
    }
}
