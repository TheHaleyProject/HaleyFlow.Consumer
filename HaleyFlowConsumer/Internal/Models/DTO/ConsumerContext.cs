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
        /// <summary>
        /// Event code to trigger on success — from the policy's complete.success field.
        /// Null if the policy rule has no success event defined.
        /// Use AutoTransitionAsync to fire this without boilerplate.
        /// </summary>
        public int? OnSuccessEvent { get; init; }
        /// <summary>
        /// Event code to trigger on failure — from the policy's complete.failure field.
        /// Null if the policy rule has no failure event defined.
        /// Use AutoTransitionAsync to fire this without boilerplate.
        /// </summary>
        public int? OnFailureEvent { get; init; }
        /// <summary>
        /// How the engine expects this transition to be processed.
        /// NormalRun: run handler + auto-transition as normal.
        /// ValidationMode: run handler + ACK result, but do NOT auto-transition (hooks are in progress; engine drives).
        /// TransitionMode: skip handler entirely; engine has already run all hooks, just fire the next event code.
        /// </summary>
        public TransitionDispatchMode DispatchMode { get; init; } = TransitionDispatchMode.NormalRun;
        public CancellationToken CancellationToken { get; init; }
    }
}
