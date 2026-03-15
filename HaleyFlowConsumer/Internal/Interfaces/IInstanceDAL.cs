using Haley.Models;

namespace Haley.Internal {
    /// <summary>
    /// Manages the <c>instance</c> table — mirrors the engine's workflow instance on the consumer side.
    /// </summary>
    internal interface IInstanceDAL {
        /// <summary>
        /// Inserts a new instance row or returns the existing row id when the GUID is already present.
        /// Returns the surrogate row id.
        /// </summary>
        Task<long> UpsertAsync(InstanceRecord record, DbExecutionLoad load = default);

        Task<InstanceRecord?> GetByIdAsync(long id, DbExecutionLoad load = default);
        Task<InstanceRecord?> GetByGuidAsync(string instanceGuid, DbExecutionLoad load = default);

        Task<DbRows> ListPagedAsync(ConsumerInstanceFilter filter, DbExecutionLoad load = default);

        /// <summary>Returns all instances for the given entity_guid, ordered newest first.</summary>
        Task<DbRows> GetByEntityAsync(string entityGuid, DbExecutionLoad load = default);
    }
}
