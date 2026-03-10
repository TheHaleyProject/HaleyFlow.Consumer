using System.Reflection;

namespace Haley.Abstractions {
    public interface IWorkFlowConsumerInitiatorService {
        IWorkFlowConsumerInitiatorService RegisterAssembly(Assembly assembly);
        IWorkFlowConsumerInitiatorService RegisterAssembly(string assemblyName);
        Task EnsureHostInitializedAsync(CancellationToken ct = default);
        Task<IWorkFlowConsumerService> GetConsumerAsync(CancellationToken ct = default);
        Task StopAsync(CancellationToken ct = default);
    }
}
