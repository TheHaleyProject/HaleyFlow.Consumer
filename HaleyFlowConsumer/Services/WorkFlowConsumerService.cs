using Haley.Abstractions;
using Haley.Internal;
using Haley.Models;

namespace Haley.Services {
    internal sealed class WorkFlowConsumerService : IWorkFlowConsumerService {
        private readonly IConsumerServiceDAL _dal;
        public WorkFlowConsumerService(IConsumerServiceDAL dal) => _dal = dal;

        public Task<DbRows> ListWorkflowsAsync(int skip, int take, CancellationToken ct = default)
            => _dal.Workflow.ListPagedAsync(skip, take, new DbExecutionLoad(ct));

        public Task<DbRows> ListInboxAsync(int? status, int skip, int take, CancellationToken ct = default)
            => _dal.Inbox.ListPagedAsync(status, skip, take, new DbExecutionLoad(ct));

        public Task<DbRows> ListOutboxAsync(int? status, int skip, int take, CancellationToken ct = default)
            => _dal.Outbox.ListPagedAsync(status, skip, take, new DbExecutionLoad(ct));

        public Task<long> CountPendingInboxAsync(CancellationToken ct = default)
            => _dal.Inbox.CountPendingAsync(new DbExecutionLoad(ct));

        public Task<long> CountPendingOutboxAsync(CancellationToken ct = default)
            => _dal.Outbox.CountPendingAsync(new DbExecutionLoad(ct));
    }
}
