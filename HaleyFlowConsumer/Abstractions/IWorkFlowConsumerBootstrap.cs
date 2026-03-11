using System.Reflection;

namespace Haley.Abstractions {
    public interface IWorkFlowConsumerBootstrap {
        IWorkFlowConsumerBootstrap RegisterAssembly(Assembly assembly);
        IWorkFlowConsumerBootstrap RegisterAssembly(string assemblyName);
        Task EnsureHostInitializedAsync(CancellationToken ct = default);
        Task<IWorkFlowConsumerProcessor> GetConsumerAsync(CancellationToken ct = default);
        Task StopAsync(CancellationToken ct = default);
    }
}
