using Haley.Enums;

namespace Haley.Services {
    public sealed class ConsumerServiceOptions {
        public long ConsumerId { get; set; }
        /// <summary>How many events to pull per poll cycle.</summary>
        public int BatchSize { get; set; } = 20;
        /// <summary>ACK status to query for due events (typically Pending=1).</summary>
        public int AckStatus { get; set; } = 1;
        /// <summary>Consumer TTL window in seconds (engine-side check).</summary>
        public int TtlSeconds { get; set; } = 120;
        /// <summary>Delay between poll cycles when no events are found.</summary>
        public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);
        /// <summary>Delay between outbox retry cycles.</summary>
        public TimeSpan OutboxInterval { get; set; } = TimeSpan.FromSeconds(15);
        /// <summary>How long to wait before re-queuing a failed outbox ACK.</summary>
        public TimeSpan OutboxRetryDelay { get; set; } = TimeSpan.FromMinutes(2);
        /// <summary>Default upgrade policy for new instances (can be overridden per entity if needed).</summary>
        public HandlerUpgrade DefaultHandlerUpgrade { get; set; } = HandlerUpgrade.Pinned;
    }
}