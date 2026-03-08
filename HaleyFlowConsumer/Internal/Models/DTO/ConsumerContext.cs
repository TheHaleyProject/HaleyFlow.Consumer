using Haley.Enums;
using System.Threading;

namespace Haley.Models {
    /// <summary>
    /// Carries consumer-side context for a single dispatched event.
    /// Passed to every handler method on <see cref="Haley.Abstractions.LifeCycleWrapper"/>.
    /// </summary>
    public sealed class ConsumerContext {
        /// <summary>Consumer-local workflow row id.</summary>
        public long WfId { get; init; }
        /// <summary>Engine-assigned consumer id for this process.</summary>
        public long ConsumerId { get; init; }
        /// <summary>Engine-issued ACK guid for this event.</summary>
        public string AckGuid { get; init; } = string.Empty;
        /// <summary>Handler version pinned for this entity instance.</summary>
        public int HandlerVersion { get; init; }
        /// <summary>Whether this instance allows handler version upgrades.</summary>
        public HandlerUpgrade HandlerUpgrade { get; init; }
        /// <summary>
        /// How many times this hook has been dispatched, including this delivery.
        /// Always 1 for transition events. Use this to detect reruns (e.g. after a ReopenAsync).
        /// </summary>
        public int RunCount { get; init; } = 1;
        public CancellationToken CancellationToken { get; init; }
    }
}
