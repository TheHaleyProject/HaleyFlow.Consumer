using Haley.Enums;
using Haley.Models;
using Haley.Utils;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
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
    /// ENGINE ACCESS FROM WRAPPERS:
    /// Wrappers that need to call engine-side operations (TriggerAsync, UpsertRuntimeAsync, etc.)
    /// use the protected <see cref="Engine"/> property. This is populated by the consumer manager
    /// after the wrapper is activated, before dispatch — exactly like the step DAL fields.
    /// No constructor parameter is needed; wrappers constructor-inject only their own app services.
    ///
    /// ACK OWNERSHIP:
    /// Wrappers normally do not call AckAsync directly. A handler returns AckOutcome and the
    /// ConsumerManager writes the ACK using that outcome. This keeps ACK persistence and
    /// retry behavior centralized in one runtime component.
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
    ///
    /// TYPICAL HANDLER FLOW:
    ///   1) Read event/context and perform domain business action (application DB/API/email).
    ///   2) Optionally write consumer-side idempotency/audit data (inbox_step/business_action).
    ///   3) Optionally call engine APIs (for example TriggerAsync for next transition, or
    ///      UpsertRuntimeAsync to add runtime traces visible on engine timeline views).
    ///   4) Return AckOutcome (Processed/Retry/Failed); ConsumerManager persists ACK result.
    /// </summary>
    public abstract class LifeCycleWrapper {

        /// <summary>For wrappers that only process events and never call back to the engine.</summary>
        protected LifeCycleWrapper() { }

        // ── Post-construction injection ────────────────────────────────────────
        // These fields are NOT injected via the constructor. The consumer manager resolves
        // (or activates) the wrapper first, then sets these fields immediately before calling
        // any dispatch method. This keeps wrapper constructors free of framework concerns —
        // subclasses only constructor-inject their own application services (repositories,
        // HTTP clients, etc.) and the framework supplies engine access + DALs transparently.
        internal ILifeCycleExecution? _engine;
        internal IConsumerInboxStepDAL? _stepDal;
        internal IConsumerBusinessActionDAL? _businessActionDal;

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

        protected static string PickEvent(string? preferred, string fallback)
        => !string.IsNullOrWhiteSpace(preferred) ? preferred : fallback;

        protected async Task<bool> IsBusinessActionCompletedAsync(ConsumerContext ctx, long defId, string entityId, int actionCode) {
            if (string.IsNullOrWhiteSpace(entityId)) return false;
            var row = await BusinessActionDal.GetByKeyAsync(
                ctx.ConsumerId, defId, entityId, actionCode,
                new DbExecutionLoad(ctx.CancellationToken));
            return row?.Status == BusinessActionStatus.Completed;
        }

        protected Task<BusinessActionRecord?> GetBusinessActionAsync(ConsumerContext ctx, long defId, string entityId, int actionCode) {
            return BusinessActionDal.GetByKeyAsync(
                ctx.ConsumerId, defId, entityId, actionCode,
                new DbExecutionLoad(ctx.CancellationToken));
        }

        protected async Task<BusinessActionExecutionResult> ExecuteBusinessActionAsync(ConsumerContext ctx, long defId, string entityId, int actionCode, Func<CancellationToken, Task<object?>> action, BusinessActionExecutionMode mode = BusinessActionExecutionMode.SkipIfCompleted) {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (defId <= 0) throw new ArgumentOutOfRangeException(nameof(defId));
            if (string.IsNullOrWhiteSpace(entityId)) throw new ArgumentNullException(nameof(entityId));
            if (actionCode <= 0) throw new ArgumentOutOfRangeException(nameof(actionCode));

            var load = new DbExecutionLoad(ctx.CancellationToken);
            var existing = await BusinessActionDal.GetByKeyAsync(ctx.ConsumerId, defId, entityId, actionCode, load);

            if (mode == BusinessActionExecutionMode.SkipIfCompleted &&
                existing != null &&
                existing.Status == BusinessActionStatus.Completed) {
                return new BusinessActionExecutionResult {
                    ActionId = existing.Id,
                    Executed = false,
                    AlreadyCompleted = true,
                    ResultJson = existing.ResultJson
                };
            }

            var actionId = existing?.Id ?? await BusinessActionDal.UpsertReturnIdAsync(
                ctx.ConsumerId,
                defId,
                entityId,
                actionCode,
                BusinessActionStatus.Running,
                load);

            if (existing != null) {
                await BusinessActionDal.SetRunningAsync(actionId, load);
            }

            try {
                var payload = await action(ctx.CancellationToken);
                var resultJson = ToResultJson(payload);
                await BusinessActionDal.SetCompletedAsync(actionId, resultJson, load);
                return new BusinessActionExecutionResult {
                    ActionId = actionId,
                    Executed = true,
                    AlreadyCompleted = false,
                    ResultJson = resultJson
                };
            } catch (OperationCanceledException) when (ctx.CancellationToken.IsCancellationRequested) {
                throw;
            } catch (Exception ex) {
                var errorJson = ToResultJson(new { error = ex.Message, type = ex.GetType().Name });
                try {
                    await BusinessActionDal.SetFailedAsync(actionId, errorJson, load);
                } catch {
                    // Best effort only; original exception is more important than audit write failures.
                }
                throw;
            }
        }

        protected static bool ReadDecisionFromResultJson(string? resultJson, bool defaultValue = true) {
            if (string.IsNullOrWhiteSpace(resultJson)) return defaultValue;
            try {
                using var doc = JsonDocument.Parse(resultJson);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return defaultValue;
                return doc.RootElement.GetBool("decision") ?? defaultValue;
            } catch {
                return defaultValue;
            }
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

        private IConsumerBusinessActionDAL BusinessActionDal =>
            _businessActionDal ?? throw new InvalidOperationException("LifeCycleWrapper not initialized by ConsumerService (BusinessAction DAL missing).");

        /// <summary>
        /// Access the engine to trigger events, fetch timelines, upsert runtime logs, etc.
        /// Populated by the consumer manager before dispatch — always available inside handler methods.
        /// </summary>
        protected ILifeCycleExecution Engine =>
            _engine ?? throw new InvalidOperationException("Engine was not injected by the consumer manager. This property is only valid inside handler methods dispatched by WorkFlowConsumerManager.");

        private static string? ToResultJson(object? value) {
            if (value == null) return null;
            if (value is string s) return s;
            return JsonSerializer.Serialize(value);
        }

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
        private static THandler? PickBestHandler<THandler>(List<(int MinVersion, THandler Handler)> candidates, int handlerVersion) where THandler : class {
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

