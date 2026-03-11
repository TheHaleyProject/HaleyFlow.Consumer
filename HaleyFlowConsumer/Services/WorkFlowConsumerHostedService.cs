using Haley.Abstractions;
using Microsoft.Extensions.Hosting;

namespace Haley.Services {
    internal sealed class WorkFlowConsumerHostedService : IHostedService {
        private readonly IWorkFlowConsumerBootstrap _bootstrap;

        public WorkFlowConsumerHostedService(IWorkFlowConsumerBootstrap bootstrap) {
            _bootstrap = bootstrap ?? throw new ArgumentNullException(nameof(bootstrap));
        }

        public Task StartAsync(CancellationToken cancellationToken)
            => _bootstrap.EnsureHostInitializedAsync(cancellationToken);

        public Task StopAsync(CancellationToken cancellationToken)
            => _bootstrap.StopAsync(cancellationToken);
    }
}
