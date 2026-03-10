using Haley.Abstractions;
using Microsoft.Extensions.Hosting;

namespace Haley.Services {
    internal sealed class WorkFlowConsumerHostedService : IHostedService {
        private readonly IWorkFlowConsumerInitiatorService _initiator;

        public WorkFlowConsumerHostedService(IWorkFlowConsumerInitiatorService initiator) {
            _initiator = initiator ?? throw new ArgumentNullException(nameof(initiator));
        }

        public Task StartAsync(CancellationToken cancellationToken)
            => _initiator.EnsureHostInitializedAsync(cancellationToken);

        public Task StopAsync(CancellationToken cancellationToken)
            => _initiator.StopAsync(cancellationToken);
    }
}
