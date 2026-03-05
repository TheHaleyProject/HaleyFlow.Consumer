using Haley.Enums;

namespace Haley.Abstractions {
    /// <summary>
    /// Abstraction over the event source (engine). Implement this to wire the consumer
    /// to the engine directly (via IAckManager) or via HTTP.
    /// </summary>
    public interface ILifeCycleEventFeed {
        Task<IReadOnlyList<ILifeCycleDispatchItem>> GetDueTransitionsAsync(long consumerId, int ackStatus, int ttlSeconds, int skip, int take, CancellationToken ct = default);
        Task<IReadOnlyList<ILifeCycleDispatchItem>> GetDueHooksAsync(long consumerId, int ackStatus, int ttlSeconds, int skip, int take, CancellationToken ct = default);
        Task AckAsync(long consumerId, string ackGuid, AckOutcome outcome, CancellationToken ct = default);
    }
}
