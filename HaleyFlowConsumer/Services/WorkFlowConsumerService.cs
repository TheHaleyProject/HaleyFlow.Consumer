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

                // Registration is owned by WorkFlowConsumerProcessor.StartAsync:
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

        public async Task<DbRows> ListWorkflowsAsync(int skip, int take, CancellationToken ct = default) {
            var consumer = await GetConsumerAsync(ct);
            return await consumer.ListWorkflowsAsync(skip, take, ct);
        }

        public async Task<DbRows> ListInboxAsync(int? status, int skip, int take, CancellationToken ct = default) {
            var consumer = await GetConsumerAsync(ct);
            return await consumer.ListInboxAsync(status, skip, take, ct);
        }

        public async Task<DbRows> ListOutboxAsync(int? status, int skip, int take, CancellationToken ct = default) {
            var consumer = await GetConsumerAsync(ct);
            return await consumer.ListOutboxAsync(status, skip, take, ct);
        }

        public async Task<long> CountPendingInboxAsync(CancellationToken ct = default) {
            var consumer = await GetConsumerAsync(ct);
            return await consumer.CountPendingInboxAsync(ct);
        }

        public async Task<long> CountPendingOutboxAsync(CancellationToken ct = default) {
            var consumer = await GetConsumerAsync(ct);
            return await consumer.CountPendingOutboxAsync(ct);
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
