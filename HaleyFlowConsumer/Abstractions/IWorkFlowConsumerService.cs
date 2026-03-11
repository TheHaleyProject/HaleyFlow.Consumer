using Haley.Models;

namespace Haley.Abstractions {
    public interface IWorkFlowConsumerService {
        Task<DbRows> ListWorkflowsAsync(int skip, int take, CancellationToken ct = default);
        Task<DbRows> ListInboxAsync(int? status, int skip, int take, CancellationToken ct = default);
        Task<DbRows> ListOutboxAsync(int? status, int skip, int take, CancellationToken ct = default);
        Task<long> CountPendingInboxAsync(CancellationToken ct = default);
        Task<long> CountPendingOutboxAsync(CancellationToken ct = default);
    }
}
