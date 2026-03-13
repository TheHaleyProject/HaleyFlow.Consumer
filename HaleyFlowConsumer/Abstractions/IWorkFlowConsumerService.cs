using Haley.Models;
using System.Reflection;

namespace Haley.Abstractions {
    public interface IWorkFlowConsumerService {
        IWorkFlowConsumerService RegisterAssembly(Assembly assembly);
        IWorkFlowConsumerService RegisterAssembly(string assemblyName);
        Task EnsureHostInitializedAsync(CancellationToken ct = default);
        Task<IWorkFlowConsumerManager> GetConsumerAsync(CancellationToken ct = default);
        Task StopAsync(CancellationToken ct = default);
        Task<DbRows> ListWorkflowsAsync(int skip, int take, CancellationToken ct = default);
        Task<DbRows> ListInboxAsync(int? status, int skip, int take, CancellationToken ct = default);
        Task<DbRows> ListOutboxAsync(int? status, int skip, int take, CancellationToken ct = default);
        Task<long> CountPendingInboxAsync(CancellationToken ct = default);
        Task<long> CountPendingOutboxAsync(CancellationToken ct = default);
    }
}
