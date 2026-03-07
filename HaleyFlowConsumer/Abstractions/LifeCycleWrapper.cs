using Haley.Enums;
using Haley.Models;
using Haley.Utils;
using System.Linq.Expressions;
using System.Reflection;
using Haley.Internal;
using static Haley.Internal.KeyConstants;

namespace Haley.Abstractions {

    /// <summary>
    /// The base class every workflow event handler must inherit from.
    ///
    /// THE BIG PICTURE:
    /// When the engine fires a lifecycle event (a state transition or a hook emission), the
    /// consumer service receives it, looks up which <see cref="LifeCycleWrapper"/> subclass is
    /// registered for that workflow definition, activates an instance from DI, and calls
    /// DispatchTransitionAsync or DispatchHookAsync. The wrapper then routes the call to the
    /// correct handler method on the subclass.
    ///
    /// Developer contract:
    ///   1. Inherit LifeCycleWrapper.
    ///   2. Decorate the class with [LifeCycleDefinition("my-definition-name")].
    ///   3. Write handler methods and decorate them with [TransitionHandler(eventCode)] or
    ///      [HookHandler("route-name")].
    ///   4. Implement OnUnhandledTransitionAsync and OnUnhandledHookAsync for events that
    ///      don't match any decorated handler — typically you return AckOutcome.Processed
    ///      to acknowledge gracefully, or AckOutcome.Failed if you consider an unknown event
    ///      a programming error.
    ///
    /// DEPENDENCY INJECTION:
    /// The wrapper is resolved from DI per dispatch, so you can constructor-inject your own
    /// services (repositories, HTTP clients, etc.) normally. LifeCycleWrapper itself has no
    /// DI requirements — the only "injection" that happens after construction is the internal
    /// assignment of <c>_stepDal</c> by the consumer service (see below).
    ///
    /// STEP TRACKING:
    /// The protected StartStep/CompleteStep/FailStep/IsStepCompleted helpers let handlers
    /// record fine-grained progress into the consumer's inbox_step table. This provides
    /// idempotency: if a handler is retried (because the process crashed before ACKing),
    /// it can check IsStepCompletedAsync before re-doing expensive work. Think of steps as
    /// lightweight checkpoints within a single handler invocation.
    ///
    /// HANDLER VERSIONING:
    /// Each handler method can declare a minimum version via [TransitionHandler(minVersion: 2)].
    /// The dispatch layer picks the highest minVersion that is still <= the resolved handler
    /// version for this event. This lets you ship new logic for new instances while old in-flight
    /// instances continue to use the old handler. See PickBestHandler and WrapperRegistry for
    /// how the version is resolved.
    /// </summary>
    public abstract class LifeCycleWrapper {

        // ── Step DAL injection ─────────────────────────────────────────────────
        // Not injected via the constructor because the wrapper is resolved from DI before
        // ConsumerService knows which event it is handling (and therefore which step table
        // connection to use). ConsumerService sets this field immediately after activation,
        // before calling any dispatch method. The public API exposes it only through the
        // protected helper methods below so subclasses can't accidentally misuse the DAL.
        internal IConsumerInboxStepDAL? _stepDal;

        // ── Step tracking helpers ──────────────────────────────────────────────
        // These are the recommended way for handler code to record progress. The step_code
        // is a developer-defined integer (e.g. an enum value) that identifies a logical
        // unit of work within the handler. Typical use:
        //
        //   if (!await IsStepCompletedAsync(ctx, Steps.SendEmail)) {
        //       await StartStepAsync(ctx, Steps.SendEmail);
        //       await emailService.SendAsync(...);
        //       await CompleteStepAsync(ctx, Steps.SendEmail);
        //   }
        //
        // If the process crashes after SendEmail but before CompleteStep, the next retry
        // will find the step in Running state (not Completed) and will re-send the email.
        // Design your external operations to be idempotent when using this pattern.

        protected Task StartStepAsync(ConsumerContext ctx, int stepCode)
            => StepDal.UpsertStepAsync(ctx.WfId, stepCode, InboxStepStatus.Running,
                load: new DbExecutionLoad(ctx.CancellationToken));

        protected Task CompleteStepAsync(ConsumerContext ctx, int stepCode, string? result = null)
            => StepDal.UpsertStepAsync(ctx.WfId, stepCode, InboxStepStatus.Completed, result: result,
                load: new DbExecutionLoad(ctx.CancellationToken));

        protected Task FailStepAsync(ConsumerContext ctx, int stepCode, string? error = null)
            => StepDal.UpsertStepAsync(ctx.WfId, stepCode, InboxStepStatus.Failed, error: error,
                load: new DbExecutionLoad(ctx.CancellationToken));

        protected async Task<bool> IsStepCompletedAsync(ConsumerContext ctx, int stepCode) {
            var row = await StepDal.GetStepAsync(ctx.WfId, stepCode,
                new DbExecutionLoad(ctx.CancellationToken));
            return row != null && row.GetInt(KEY_STATUS) == (int)InboxStepStatus.Completed;
        }

        // ── Unhandled event fallbacks ──────────────────────────────────────────
        // These are deliberately abstract (not virtual with a default) because we don't
        // want to silently swallow events that were not explicitly handled. An unhandled
        // event almost always means either:
        //   a) The developer forgot to write a handler method for a new state transition.
        //   b) The engine fired an event the consumer wasn't expecting (definition mismatch).
        // In either case the developer must decide: is this OK (return Processed) or is it
        // a bug (return Failed / throw)? We cannot make that decision for them.

        protected abstract Task<AckOutcome> OnUnhandledTransitionAsync(ILifeCycleTransitionEvent evt, ConsumerContext ctx);
        protected abstract Task<AckOutcome> OnUnhandledHookAsync(ILifeCycleHookEvent evt, ConsumerContext ctx);

        // ── Internal dispatch entry points ─────────────────────────────────────
        // Called by ConsumerService after DI activation. These are internal so that
        // subclasses cannot accidentally call them — dispatch is always initiated by
        // the consumer service, never by user code.

        internal Task<AckOutcome> DispatchTransitionAsync(ILifeCycleTransitionEvent evt, ConsumerContext ctx) {
            // The DispatchCacheStore builds (once, lazily, per wrapper type) a dictionary
            // from event code → list of (minVersion, handler delegate) entries. Building
            // the cache uses reflection to scan the subclass for [TransitionHandler] methods
            // and compile them to strongly-typed delegates — expensive the first time, but
            // the result is cached forever so every subsequent dispatch is essentially a
            // dictionary lookup + delegate invoke.
            var cache = DispatchCacheStore.GetOrBuild(GetType());
            if (cache.Transitions.TryGetValue(evt.EventCode, out var candidates)) {
                var handler = PickBestHandler(candidates, ctx.HandlerVersion);
                if (handler != null) return handler(this, evt, ctx);
            }
            // No decorated handler matched — fall through to the developer-provided fallback.
            return OnUnhandledTransitionAsync(evt, ctx);
        }

        internal Task<AckOutcome> DispatchHookAsync(ILifeCycleHookEvent evt, ConsumerContext ctx) {
            // Hooks are keyed by route name (the "route" field in the policy emit JSON).
            // An empty route is valid — it means the hook was emitted without a route
            // qualifier, and a handler decorated with [HookHandler("")] (or [HookHandler]
            // with no arguments) will catch it. This pattern is used for "catch-all" hook
            // handlers on small wrappers that only handle one kind of hook.
            var cache = DispatchCacheStore.GetOrBuild(GetType());
            var key = evt.Route ?? string.Empty;
            if (cache.Hooks.TryGetValue(key, out var candidates)) {
                var handler = PickBestHandler(candidates, ctx.HandlerVersion);
                if (handler != null) return handler(this, evt, ctx);
            }
            return OnUnhandledHookAsync(evt, ctx);
        }

        // ── Internal helpers ───────────────────────────────────────────────────

        private IConsumerInboxStepDAL StepDal =>
            _stepDal ?? throw new InvalidOperationException("LifeCycleWrapper not initialized by ConsumerService.");

        /// <summary>
        /// Version-aware handler selection.
        ///
        /// The candidates list contains every decorated handler for this event code / route,
        /// ordered by their declared minVersion. We want the "most specific" handler that
        /// still applies to the current instance's handler version:
        ///
        ///   - A handler with minVersion=1 applies to all instances (version >= 1).
        ///   - A handler with minVersion=3 applies only to instances with version >= 3.
        ///
        /// If an instance has handlerVersion=2 and there are handlers at minVersion=1 and
        /// minVersion=3, we pick minVersion=1 (the highest that is still <= 2).
        ///
        /// This lets developers add improved handler logic for new workflow instances
        /// (decorated with minVersion=3) while old instances continue to use the minVersion=1
        /// path — no migration, no downtime.
        ///
        /// Returns null if no candidate's minVersion is <= handlerVersion (which shouldn't
        /// happen in practice if the developer registered handlers correctly, but the caller
        /// then falls through to OnUnhandled as a safety net).
        /// </summary>
        private static THandler? PickBestHandler<THandler>(
            List<(int MinVersion, THandler Handler)> candidates, int handlerVersion)
            where THandler : class {
            // Pick highest MinVersion that is <= handlerVersion
            (int MinVersion, THandler Handler)? best = null;
            foreach (var c in candidates) {
                if (c.MinVersion <= handlerVersion) {
                    if (best == null || c.MinVersion > best.Value.MinVersion)
                        best = c;
                }
            }
            return best?.Handler;
        }
    }
}


