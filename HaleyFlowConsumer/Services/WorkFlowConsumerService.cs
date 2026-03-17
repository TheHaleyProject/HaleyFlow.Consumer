using Haley.Abstractions;
using Haley.Models;
using Haley.Utils;
using System.Reflection;

namespace Haley.Services {
    public sealed class WorkFlowConsumerService : IWorkFlowConsumerService, IAsyncDisposable {
        private readonly ConsumerServiceOptions _options;
        private readonly IAdapterGateway _agw;
        private readonly ILifeCycleEngineProxy _engineProxy;
        private readonly IServiceProvider _serviceProvider;
        private readonly SemaphoreSlim _initLock = new(1, 1);
        private readonly HashSet<Assembly> _registeredAssemblies = new();

        private IWorkFlowConsumerManager? _consumer;
        private bool _runtimeStarted;

        public WorkFlowConsumerService(ConsumerServiceOptions options, IAdapterGateway agw, ILifeCycleEngineProxy engineProxy, IServiceProvider serviceProvider) {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _agw = agw ?? throw new ArgumentNullException(nameof(agw));
            _engineProxy = engineProxy ?? throw new ArgumentNullException(nameof(engineProxy));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public IWorkFlowConsumerService RegisterAssembly(Assembly assembly) {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));

            lock (_registeredAssemblies) {
                _registeredAssemblies.Add(assembly);
            }

            _consumer?.RegisterAssembly(assembly);
            return this;
        }

        public IWorkFlowConsumerService RegisterAssembly(string assemblyName) {
            if (string.IsNullOrWhiteSpace(assemblyName)) throw new ArgumentException("Assembly name is required.", nameof(assemblyName));
            var asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => string.Equals(a.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase))
                ?? Assembly.Load(assemblyName);
            return RegisterAssembly(asm);
        }

        public async Task EnsureHostInitializedAsync(CancellationToken ct = default) {
            if (_runtimeStarted) return;

            await _initLock.WaitAsync(ct);
            try {
                if (_runtimeStarted) return;

                if (_consumer == null) {
                    if (string.IsNullOrWhiteSpace(_options.ConsumerAdapterKey))
                        throw new InvalidOperationException("ConsumerAdapterKey is required to initialize WorkFlowConsumerService.");

                    var maker = new WorkFlowConsumerMaker()
                        .WithAdapterKey(_options.ConsumerAdapterKey)
                        .WithProvider(_serviceProvider);

                    maker.EngineProxy = _engineProxy;
                    maker.Options = _options;
                    _consumer = await maker.Build(_agw);

                    RegisterConfiguredAssemblies(_consumer);
                    RegisterRuntimeAssemblies(_consumer);
                }

                // Registration is owned by WorkFlowConsumerManager.StartAsync:
                // it registers environment + consumer identity before loops start.
                await _consumer.StartAsync(ct);
                _runtimeStarted = true;
            } finally {
                _initLock.Release();
            }
        }

        public async Task<IWorkFlowConsumerManager> GetConsumerAsync(CancellationToken ct = default) {
            await EnsureHostInitializedAsync(ct);
            return _consumer!;
        }

        public async Task StopAsync(CancellationToken ct = default) {
            await _initLock.WaitAsync(ct);
            try {
                if (!_runtimeStarted || _consumer == null) return;
                await _consumer.StopAsync(ct);
                _runtimeStarted = false;
            } finally {
                _initLock.Release();
            }
        }

        public async Task<DbRows> ListInstancesAsync(ConsumerInstanceFilter filter, CancellationToken ct = default) {
            var consumer = await GetConsumerAsync(ct);
            return await consumer.ListInstancesAsync(filter, ct);
        }

        public async Task<DbRows> ListInboxAsync(ConsumerInboxFilter filter, CancellationToken ct = default) {
            var consumer = await GetConsumerAsync(ct);
            return await consumer.ListInboxAsync(filter, ct);
        }

        public async Task<DbRows> ListInboxStatusesAsync(ConsumerInboxStatusFilter filter, CancellationToken ct = default) {
            var consumer = await GetConsumerAsync(ct);
            return await consumer.ListInboxStatusesAsync(filter, ct);
        }

        public async Task<DbRows> ListOutboxAsync(ConsumerOutboxFilter filter, CancellationToken ct = default) {
            var consumer = await GetConsumerAsync(ct);
            return await consumer.ListOutboxAsync(filter, ct);
        }

        public async Task<long> CountPendingInboxAsync(CancellationToken ct = default) {
            var consumer = await GetConsumerAsync(ct);
            return await consumer.CountPendingInboxAsync(ct);
        }

        public async Task<long> CountPendingOutboxAsync(CancellationToken ct = default) {
            var consumer = await GetConsumerAsync(ct);
            return await consumer.CountPendingOutboxAsync(ct);
        }

        public async Task<ConsumerTimeline> GetConsumerTimelineAsync(string instanceGuid, CancellationToken ct = default) {
            var consumer = await GetConsumerAsync(ct);
            return await consumer.GetConsumerTimelineAsync(instanceGuid, ct);
        }

        public async Task<string?> GetConsumerTimelineHtmlAsync(string instanceGuid, string? displayName = null, string? color = null, CancellationToken ct = default) {
            var consumer = await GetConsumerAsync(ct);
            var timeline = await consumer.GetConsumerTimelineAsync(instanceGuid, ct);
            if (timeline == null || timeline.Instance == null) return null;
            return ControlBoardTLR.Render(timeline, consumer.ConsumerGuid, displayName, color);
        }

        public async Task<LifeCycleTriggerResult> CreateWorkflowAsync(string entityGuid, string defName, CreateWorkflowRequest request, CancellationToken ct = default) {
            if (string.IsNullOrWhiteSpace(entityGuid)) throw new ArgumentException("entityGuid is required.", nameof(entityGuid));
            if (string.IsNullOrWhiteSpace(defName)) throw new ArgumentException("defName is required.", nameof(defName));
            if (request == null) throw new ArgumentNullException(nameof(request));

            // All engine calls go through the proxy — never directly to the engine.
            // InProcessEngineProxy delegates to the in-process engine; an HttpEngineProxy
            // would POST to the remote engine API. The service doesn't need to know which.
            var triggerReq = new LifeCycleTriggerRequest {
                EnvCode = _options.EnvCode,
                DefName = defName,
                EntityId = entityGuid,
                Event = request.Event,
                Actor = request.Actor,
                Metadata = request.Metadata,
                Payload = request.Payload,
                SkipAckGate = request.SkipAckGate
            };
            var result = await _engineProxy.TriggerAsync(triggerReq, ct);

            // Upsert the consumer-side instance mirror.
            var consumer = await GetConsumerAsync(ct);
            await consumer.RecordInstanceAsync(entityGuid, defName, result, ct);
            return result;
        }

        public Task<LifeCycleTriggerResult> TriggerAsync(LifeCycleTriggerRequest request, CancellationToken ct = default) {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return _engineProxy.TriggerAsync(request, ct);
        }

        public async Task<DbRows> GetInstancesByEntityAsync(string entityGuid, CancellationToken ct = default) {
            var consumer = await GetConsumerAsync(ct);
            return await consumer.GetInstancesByEntityAsync(entityGuid, ct);
        }

        public async ValueTask DisposeAsync() {
            try { await StopAsync(CancellationToken.None); } catch { }
            _initLock.Dispose();
        }

        private void RegisterConfiguredAssemblies(IWorkFlowConsumerManager consumer) {
            if (_options.WrapperAssemblies == null || _options.WrapperAssemblies.Count == 0) return;
            for (var i = 0; i < _options.WrapperAssemblies.Count; i++) {
                var asmName = _options.WrapperAssemblies[i];
                if (string.IsNullOrWhiteSpace(asmName)) continue;
                consumer.RegisterAssembly(asmName.Trim());
            }
        }

        private void RegisterRuntimeAssemblies(IWorkFlowConsumerManager consumer) {
            Assembly[] snapshot;
            lock (_registeredAssemblies) {
                snapshot = _registeredAssemblies.ToArray();
            }

            for (var i = 0; i < snapshot.Length; i++) {
                consumer.RegisterAssembly(snapshot[i]);
            }
        }
    }
}
