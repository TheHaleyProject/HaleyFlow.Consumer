using Haley.Enums;
using System.Threading;

namespace Haley.Models {
    /// <summary>
    /// Carries consumer-side context for a single dispatched event.
    /// Passed to every handler method on <see cref="Haley.Abstractions.LifeCycleWrapper"/>.
    /// </summary>
    public sealed class ConsumerContext {
        /// <summary>Consumer-local inbox row id (inbox.id) for this delivery.</summary>
        public long InboxId { get; init; }
        /// <summary>Consumer-local instance id (instance.id) — FK scoping business actions and handler versions.</summary>
        public long InstanceId { get; init; }
        /// <summary>Instance GUID from the engine — stable cross-system reference.</summary>
        public string InstanceGuid { get; init; } = string.Empty;
        /// <summary>Entity GUID for this instance — the business entity identifier.</summary>
        public string EntityGuid { get; init; } = string.Empty;
        /// <summary>Engine-issued ACK guid for this event.</summary>
        public string AckGuid { get; init; } = string.Empty;
        /// <summary>Handler version pinned for this instance.</summary>
        public int HandlerVersion { get; init; }
        /// <summary>Whether this instance allows handler version upgrades.</summary>
        public HandlerUpgrade HandlerUpgrade { get; init; }
        /// <summary>
        /// How many times this hook has been dispatched, including this delivery.
        /// Always 1 for transition events.
        /// </summary>
        public int RunCount { get; init; } = 1;
        public CancellationToken CancellationToken { get; init; }
    }
}
