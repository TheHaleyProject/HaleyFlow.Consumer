using Haley.Abstractions;
using Haley.Enums;
using Haley.Models;
using System.Threading.Channels;

namespace Haley.Services {

    /// <summary>
    /// In-process implementation of <see cref="ILifeCycleEngineProxy"/> — the "short wire" between
    /// the engine and the consumer when they run in the same process.
    ///
    /// BACKGROUND: The consumer service is designed to be deployment-agnostic. In production the
    /// consumer typically runs in a separate process (or even a separate machine) and receives
    /// events over a message broker (Kafka, RabbitMQ, etc.). In that case a "remote" proxy
    /// implementation reads from the broker. But during development, testing, or lightweight
    /// deployments you may want the engine and the consumer in the same .NET process — that is
    /// exactly what InProcessEngineProxy enables.
    ///
    /// HOW IT WORKS:
    /// Rather than publishing to a broker and reading back from it, we subscribe directly to the
    /// engine's <see cref="IWorkFlowEngine.EventRaised"/> delegate. Every time the engine fires a
    /// lifecycle event it calls <see cref="OnEventRaised"/>, which routes the event into one of
    /// two unbounded in-memory channels — one for transition events, one for hook events.
    ///
    /// The consumer service's poll loop then calls <see cref="GetDueTransitionsAsync"/> and
    /// <see cref="GetDueHooksAsync"/> on each tick, which drain whatever items are ready in the
    /// channels. This keeps the polling contract identical whether the proxy is in-process or
    /// remote — the consumer service doesn't need to know the difference.
    ///
    /// TRADE-OFFS:
    /// - No durability: if the process crashes between the engine firing and the consumer ACKing,
    ///   the event is lost from the channel. The engine's monitor will eventually re-send it, so
    ///   at-least-once delivery is still guaranteed — just with a longer tail latency.
    /// - No fan-out: each event goes into this one channel. If you need multiple independent
    ///   consumer services in the same process, you would need multiple InProcessEngineProxy
    ///   instances, each subscribed to the engine separately.
    /// - No broker overhead: events are delivered with essentially zero latency, which makes
    ///   this ideal for integration tests where you want fast, deterministic execution.
    ///
    /// ILifeCycleConsumerBus DELEGATION:
    /// The consumer service needs to register itself, heartbeat, and ACK events — all via
    /// <see cref="ILifeCycleConsumerBus"/>. In a remote setup those calls go to a network API.
    /// Here they delegate directly to the engine. From the engine's perspective there is no
    /// difference — it just receives method calls.
    /// </summary>
    public sealed class InProcessEngineProxy : ILifeCycleEngineProxy {

        private readonly IWorkFlowEngine _engine;

        // Two separate channels: one for lifecycle state transitions, one for hook events.
        // Keeping them separate mirrors how the consumer service processes them — transitions
        // have different handler logic than hooks, so they are dispatched through different
        // code paths. Using separate channels also lets the poll loop drain each independently
        // without one kind of event blocking the other.
        private readonly Channel<ILifeCycleDispatchItem> _transitions = Channel.CreateUnbounded<ILifeCycleDispatchItem>();
        private readonly Channel<ILifeCycleDispatchItem> _hooks = Channel.CreateUnbounded<ILifeCycleDispatchItem>();

        /// <inheritdoc/>
        /// Engine notices are relayed here so the consumer service can surface them through
        /// its own NoticeRaised event without requiring a separate engine subscription.
        public event Func<LifeCycleNotice, Task>? NoticeRaised;

        public InProcessEngineProxy(IWorkFlowEngine engine) {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            // Wire up to the engine's broadcast delegate. Every event the engine raises —
            // whether from TriggerAsync, monitor resend, or hook order advancement — will
            // arrive here synchronously inside the engine's own call. We return immediately
            // (TryWrite is non-blocking) so we never hold up the engine's thread.
            _engine.EventRaised += OnEventRaised;

            // Relay engine notices through our own NoticeRaised so the consumer service
            // can surface them without a separate subscription to the engine.
            _engine.NoticeRaised += OnEngineNoticeRaised;
        }

        // ── Event ingress ──────────────────────────────────────────────────────────
        // Called by the engine on the engine's own thread/task. Must be fast and non-blocking.
        // We wrap the engine event in an InProcessDispatchItem adapter that satisfies the
        // ILifeCycleDispatchItem contract expected by the consumer service's dispatch logic,
        // then drop it into the appropriate channel. TryWrite on an unbounded channel never
        // blocks and never fails (no capacity limit), so this is safe to call from any context.

        private Task OnEventRaised(ILifeCycleEvent evt) {
            var item = new InProcessDispatchItem(evt);
            var writer = evt.Kind == LifeCycleEventKind.Transition
                ? _transitions.Writer
                : _hooks.Writer;
            writer.TryWrite(item);
            return Task.CompletedTask;
        }

        private Task OnEngineNoticeRaised(LifeCycleNotice n) {
            var h = NoticeRaised;
            if (h == null) return Task.CompletedTask;
            // Fire each subscriber as a background task — identical to engine's own FireNotice pattern.
            foreach (Func<LifeCycleNotice, Task> sub in h.GetInvocationList()) {
                var captured = sub;
                _ = Task.Run(async () => { try { await captured(n); } catch { } });
            }
            return Task.CompletedTask;
        }

        // ── Polling — drain available items; returns immediately (no blocking).
        // The consumer service's poll loop handles the delay when both return empty.
        //
        // The consumer service calls these on every poll tick. For remote proxies these
        // would issue a database or broker query with skip/take paging — here we simply
        // drain up to `take` items from the in-memory channel. The `consumerId`,
        // `ackStatus`, `ttlSeconds`, and `skip` parameters are ignored because:
        //  - consumerId / ackStatus: ACK state lives in the engine DB, not the channel.
        //  - ttlSeconds: no concept of "overdue" in an in-memory queue.
        //  - skip: channels are FIFO; you can't skip without consuming.
        // This is acceptable because in-process use is typically single-consumer and
        // items are processed immediately after being drained.

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
        // In a remote deployment these calls would go to an HTTP API or message-based
        // command bus. Here the engine IS the bus — we call it directly. Same behavior,
        // zero network hops.

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

        // ── IBlueprintImporter ─────────────────────────────────────────────────
        // Same story — in-process consumers can also push blueprint JSON directly
        // to the engine without going through a separate import API.

        public Task<long?> GetDefinitionIdAsync(int envCode, string definitionName, CancellationToken ct = default)
            => _engine.GetDefinitionIdAsync(envCode, definitionName, ct);

        public Task<long> ImportDefinitionJsonAsync(int envCode, string envDisplayName, string definitionJson, CancellationToken ct = default)
            => _engine.ImportDefinitionJsonAsync(envCode, envDisplayName, definitionJson, ct);

        public Task<long> ImportPolicyJsonAsync(int envCode, string envDisplayName, string policyJson, CancellationToken ct = default)
            => _engine.ImportPolicyJsonAsync(envCode, envDisplayName, policyJson, ct);

        // ── Private adapter ────────────────────────────────────────────────────
        // The consumer service's dispatch pipeline expects ILifeCycleDispatchItem, which
        // is the shape returned by remote proxy queries (it carries DB-level metadata like
        // ack_id, trigger count, next_due, etc.). For in-process events we don't have
        // that DB metadata — we adapt the engine's ILifeCycleEvent into the contract
        // with sensible defaults:
        //  - AckId = 0        → not used by the dispatch pipeline (only by the outbox)
        //  - AckStatus = 0    → treated as "pending" which triggers normal dispatch
        //  - TriggerCount = 1 → first (and only) in-process delivery
        //  - NextDue = null   → no scheduled retry; monitor handles any resend needed

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
