using Haley.Abstractions;
using Haley.Enums;
using Haley.Models;
using System.Threading.Channels;

namespace Haley.Services {

    /// <summary>
    /// <see cref="ILifeCycleEventFeed"/> implementation for in-process use.
    /// Subscribes to <see cref="IWorkFlowEngine.EventRaised"/> and routes events into
    /// separate channels for transitions and hooks. All <see cref="ILifeCycleConsumerBus"/>
    /// calls delegate directly to the engine.
    /// </summary>
    public sealed class InProcessEventFeed : ILifeCycleEventFeed {

        private readonly IWorkFlowEngine _engine;
        private readonly Channel<ILifeCycleDispatchItem> _transitions = Channel.CreateUnbounded<ILifeCycleDispatchItem>();
        private readonly Channel<ILifeCycleDispatchItem> _hooks = Channel.CreateUnbounded<ILifeCycleDispatchItem>();

        public InProcessEventFeed(IWorkFlowEngine engine) {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _engine.EventRaised += OnEventRaised;
        }

        private Task OnEventRaised(ILifeCycleEvent evt) {
            var item = new InProcessDispatchItem(evt);
            var writer = evt.Kind == LifeCycleEventKind.Transition
                ? _transitions.Writer
                : _hooks.Writer;
            writer.TryWrite(item);
            return Task.CompletedTask;
        }

        // ── Polling — drain available items; returns immediately (no blocking).
        // The consumer service's poll loop handles the delay when both return empty.

        public Task<IReadOnlyList<ILifeCycleDispatchItem>> GetDueTransitionsAsync(
            long consumerId, int ackStatus, int ttlSeconds, int skip, int take, CancellationToken ct = default) {
            var result = new List<ILifeCycleDispatchItem>();
            while (result.Count < take && _transitions.Reader.TryRead(out var item))
                result.Add(item);
            return Task.FromResult<IReadOnlyList<ILifeCycleDispatchItem>>(result);
        }

        public Task<IReadOnlyList<ILifeCycleDispatchItem>> GetDueHooksAsync(
            long consumerId, int ackStatus, int ttlSeconds, int skip, int take, CancellationToken ct = default) {
            var result = new List<ILifeCycleDispatchItem>();
            while (result.Count < take && _hooks.Reader.TryRead(out var item))
                result.Add(item);
            return Task.FromResult<IReadOnlyList<ILifeCycleDispatchItem>>(result);
        }

        // ── ILifeCycleConsumerBus — pure delegation to the engine ──────────────

        public Task AckAsync(long consumerId, string ackGuid, AckOutcome outcome,
            string? message = null, DateTimeOffset? retryAt = null, CancellationToken ct = default)
            => _engine.AckAsync(consumerId, ackGuid, outcome, message, retryAt, ct);

        public Task AckAsync(int envCode, string consumerGuid, string ackGuid, AckOutcome outcome,
            string? message = null, DateTimeOffset? retryAt = null, CancellationToken ct = default)
            => _engine.AckAsync(envCode, consumerGuid, ackGuid, outcome, message, retryAt, ct);

        public Task<long> RegisterConsumerAsync(int envCode, string consumerGuid, CancellationToken ct = default)
            => _engine.RegisterConsumerAsync(envCode, consumerGuid, ct);

        public Task BeatConsumerAsync(int envCode, string consumerGuid, CancellationToken ct = default)
            => _engine.BeatConsumerAsync(envCode, consumerGuid, ct);

        public Task<int> RegisterEnvironmentAsync(int envCode, string? envDisplayName, CancellationToken ct = default)
            => _engine.RegisterEnvironmentAsync(envCode, envDisplayName, ct);

        public Task<long?> GetDefinitionIdAsync(int envCode, string definitionName, CancellationToken ct = default)
            => _engine.GetDefinitionIdAsync(envCode, definitionName, ct);

        // ── IBlueprintImporter ─────────────────────────────────────────────────

        public Task<long> ImportDefinitionJsonAsync(int envCode, string envDisplayName, string definitionJson, CancellationToken ct = default)
            => _engine.ImportDefinitionJsonAsync(envCode, envDisplayName, definitionJson, ct);

        public Task<long> ImportPolicyJsonAsync(int envCode, string envDisplayName, string policyJson, CancellationToken ct = default)
            => _engine.ImportPolicyJsonAsync(envCode, envDisplayName, policyJson, ct);

        // ── Private adapter ────────────────────────────────────────────────────

        private sealed class InProcessDispatchItem : ILifeCycleDispatchItem {
            private readonly ILifeCycleEvent _event;
            public InProcessDispatchItem(ILifeCycleEvent evt) => _event = evt;

            public LifeCycleEventKind Kind => _event.Kind;
            public long AckId => 0;
            public string AckGuid => _event.AckGuid;
            public long ConsumerId => _event.ConsumerId;
            public int AckStatus => 0;
            public int TriggerCount => 1;
            public DateTime LastTrigger => DateTime.UtcNow;
            public DateTime? NextDue => null;
            public ILifeCycleEvent Event => _event;
        }
    }
}
