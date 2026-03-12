using Haley.Abstractions;
using Microsoft.Extensions.Hosting;

namespace Haley.Services {
    internal sealed class WorkFlowConsumerBootstrap : IHostedService {
        private readonly IWorkFlowConsumerService _consumerService;

        public WorkFlowConsumerBootstrap(IWorkFlowConsumerService consumerService) {
            _consumerService = consumerService ?? throw new ArgumentNullException(nameof(consumerService));
        }

        public Task StartAsync(CancellationToken cancellationToken)
            => _consumerService.EnsureHostInitializedAsync(cancellationToken);

        public Task StopAsync(CancellationToken cancellationToken)
            => _consumerService.StopAsync(cancellationToken);
    }
}
