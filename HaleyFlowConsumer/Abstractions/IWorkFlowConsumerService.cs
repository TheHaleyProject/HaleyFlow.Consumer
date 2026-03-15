using Haley.Models;
using System.Reflection;

namespace Haley.Abstractions {
    public interface IWorkFlowConsumerService {
        IWorkFlowConsumerService RegisterAssembly(Assembly assembly);
        IWorkFlowConsumerService RegisterAssembly(string assemblyName);
        Task EnsureHostInitializedAsync(CancellationToken ct = default);
        Task<IWorkFlowConsumerManager> GetConsumerAsync(CancellationToken ct = default);
        Task StopAsync(CancellationToken ct = default);

        // ── Administrative reads ──────────────────────────────────────────────
        Task<DbRows> ListWorkflowsAsync(ConsumerWorkflowFilter filter, CancellationToken ct = default);
        Task<DbRows> ListInboxAsync(ConsumerInboxFilter filter, CancellationToken ct = default);
        Task<DbRows> ListInboxStatusesAsync(ConsumerInboxStatusFilter filter, CancellationToken ct = default);
        Task<DbRows> ListOutboxAsync(ConsumerOutboxFilter filter, CancellationToken ct = default);
        Task<long> CountPendingInboxAsync(CancellationToken ct = default);
        Task<long> CountPendingOutboxAsync(CancellationToken ct = default);
        Task<ConsumerTimeline> GetConsumerTimelineAsync(string instanceGuid, CancellationToken ct = default);

        // ── Entity & Workflow management (client-facing) ──────────────────────
        /// <summary>
        /// Generates a new entity GUID, persists it, and returns it to the caller.
        /// The client stores this ID as their cross-system entity reference.
        /// </summary>
        Task<string> CreateEntityAsync(CancellationToken ct = default);

        /// <summary>
        /// Records a workflow for the given entity and immediately triggers it on the engine.
        /// Returns the engine's trigger result (including InstanceGuid, Applied, state transition).
        /// </summary>
        Task<LifeCycleTriggerResult> CreateWorkflowAsync(string entityId, string defName, CreateWorkflowRequest request, CancellationToken ct = default);

        /// <summary>
        /// Returns all workflows the entity is enrolled in across all definitions.
        /// </summary>
        Task<DbRows> GetWorkflowsByEntityAsync(string entityId, CancellationToken ct = default);
    }
}
