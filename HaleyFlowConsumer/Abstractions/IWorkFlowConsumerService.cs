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
        Task<DbRows> ListInstancesAsync(ConsumerInstanceFilter filter, CancellationToken ct = default);
        Task<DbRows> ListInboxAsync(ConsumerInboxFilter filter, CancellationToken ct = default);
        Task<DbRows> ListInboxStatusesAsync(ConsumerInboxStatusFilter filter, CancellationToken ct = default);
        Task<DbRows> ListOutboxAsync(ConsumerOutboxFilter filter, CancellationToken ct = default);
        Task<long> CountPendingInboxAsync(CancellationToken ct = default);
        Task<long> CountPendingOutboxAsync(CancellationToken ct = default);
        Task<ConsumerTimeline> GetConsumerTimelineAsync(string instanceGuid, CancellationToken ct = default);
        Task<string?> GetConsumerTimelineHtmlAsync(string instanceGuid, string? displayName = null, string? color = null, CancellationToken ct = default);

        // ── Instance management (client-facing) ──────────────────────────────
        /// <summary>
        /// Triggers a new workflow instance on the engine for the given entity GUID and records
        /// the consumer-side instance mirror. Returns the engine's trigger result.
        /// </summary>
        Task<LifeCycleTriggerResult> CreateWorkflowAsync(string entityGuid, string defName, CreateWorkflowRequest request, CancellationToken ct = default);

        /// <summary>
        /// Fires an event on an existing workflow instance (e.g. human-actor transitions such as
        /// CompanyApproved or ReviewRejected). Delegates directly to the engine proxy.
        /// </summary>
        Task<LifeCycleTriggerResult> TriggerAsync(LifeCycleTriggerRequest request, CancellationToken ct = default);

        /// <summary>
        /// Returns all instances associated with the given entity GUID across all definitions.
        /// </summary>
        Task<DbRows> GetInstancesByEntityAsync(string entityGuid, CancellationToken ct = default);
    }
}
