using Haley.Abstractions;
using Haley.Enums;
using Haley.Models;

namespace Haley.Services {

    //DeferredInProcessEngineProxy is intentionally different than InprocessEngineProxy.. Why? because this as the name suggests, the creation of the engine is deferred. 
    //Notice, _inner ? that is not created at DI registration but created only on first call. Which means, we dont' need the IWorkFlowEngine until call time.. so, may be engine is not started or not registered yes.. So, we dont have to worry about it.
    //When we make the first call, engineProxy would be created..
    //In this case, the deferred proxy becomes 'Virtual Proxy'
    public sealed class DeferredInProcessEngineProxy : ILifeCycleEngineProxy {
        private readonly IWorkFlowEngineAccessor _engineAccessor; //Not an engine instance but a provider/factory to obtain the engine
        private readonly SemaphoreSlim _initLock = new(1, 1);
        private InProcessEngineProxy? _inner;

        public event Func<LifeCycleNotice, Task>? NoticeRaised;

        public DeferredInProcessEngineProxy(IWorkFlowEngineAccessor engineAccessor) {
            _engineAccessor = engineAccessor ?? throw new ArgumentNullException(nameof(engineAccessor));
        }

        public async Task<IReadOnlyList<ILifeCycleDispatchItem>> GetDueTransitionsAsync(long consumerId, int ackStatus, int ttlSeconds, int skip, int take, CancellationToken ct = default)
            => await (await GetInnerAsync(ct)).GetDueTransitionsAsync(consumerId, ackStatus, ttlSeconds, skip, take, ct);

        public async Task<IReadOnlyList<ILifeCycleDispatchItem>> GetDueHooksAsync(long consumerId, int ackStatus, int ttlSeconds, int skip, int take, CancellationToken ct = default)
            => await (await GetInnerAsync(ct)).GetDueHooksAsync(consumerId, ackStatus, ttlSeconds, skip, take, ct);

        public async Task AckAsync(long consumerId, string ackGuid, AckOutcome outcome, string? message = null, DateTimeOffset? retryAt = null, CancellationToken ct = default)
            => await (await GetInnerAsync(ct)).AckAsync(consumerId, ackGuid, outcome, message, retryAt, ct);

        public async Task AckAsync(int envCode, string consumerGuid, string ackGuid, AckOutcome outcome, string? message = null, DateTimeOffset? retryAt = null, CancellationToken ct = default)
            => await (await GetInnerAsync(ct)).AckAsync(envCode, consumerGuid, ackGuid, outcome, message, retryAt, ct);

        public async Task<long> RegisterConsumerAsync(int envCode, string consumerGuid, CancellationToken ct = default)
            => await (await GetInnerAsync(ct)).RegisterConsumerAsync(envCode, consumerGuid, ct);

        public async Task BeatConsumerAsync(int envCode, string consumerGuid, CancellationToken ct = default)
            => await (await GetInnerAsync(ct)).BeatConsumerAsync(envCode, consumerGuid, ct);

        public async Task<int> RegisterEnvironmentAsync(int envCode, string? envDisplayName, CancellationToken ct = default)
            => await (await GetInnerAsync(ct)).RegisterEnvironmentAsync(envCode, envDisplayName, ct);

        public async Task<long?> GetDefinitionIdAsync(int envCode, string definitionName, CancellationToken ct = default)
            => await (await GetInnerAsync(ct)).GetDefinitionIdAsync(envCode, definitionName, ct);

        public async Task<long> ImportDefinitionJsonAsync(int envCode, string envDisplayName, string definitionJson, CancellationToken ct = default)
            => await (await GetInnerAsync(ct)).ImportDefinitionJsonAsync(envCode, envDisplayName, definitionJson, ct);

        public async Task<long> ImportPolicyJsonAsync(int envCode, string envDisplayName, string policyJson, CancellationToken ct = default)
            => await (await GetInnerAsync(ct)).ImportPolicyJsonAsync(envCode, envDisplayName, policyJson, ct);

        private async Task<InProcessEngineProxy> GetInnerAsync(CancellationToken ct) {
            if (_inner != null) return _inner;

            await _initLock.WaitAsync(ct);
            try {
                if (_inner != null) return _inner;
                var engine = await _engineAccessor.GetEngineAsync(ct);
                var proxy = new InProcessEngineProxy(engine);
                proxy.NoticeRaised += OnProxyNoticeRaised;
                _inner = proxy;
                return _inner;
            } finally {
                _initLock.Release();
            }
        }

        private Task OnProxyNoticeRaised(LifeCycleNotice notice) {
            var handler = NoticeRaised;
            if (handler == null) return Task.CompletedTask;
            foreach (Func<LifeCycleNotice, Task> subscriber in handler.GetInvocationList()) {
                var copy = subscriber;
                _ = Task.Run(async () => {
                    try { await copy(notice); } catch { }
                });
            }

            return Task.CompletedTask;
        }
    }
}
