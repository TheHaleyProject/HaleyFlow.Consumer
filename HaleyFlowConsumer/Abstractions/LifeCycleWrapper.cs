using Haley.Enums;
using Haley.Models;
using Haley.Utils;
using System.Reflection;
using System.Text.Json;
using Haley.Internal;

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
    ///      don't match any decorated handler.
    ///
    /// BUSINESS ACTIONS:
    /// Use ExecuteBusinessActionAsync to run idempotent side-effects. The action is scoped
    /// to the workflow instance (ctx.InstanceId + actionCode). If the action was already
    /// completed for this instance (SkipIfCompleted mode), the call returns immediately
    /// without re-executing — safe to call on handler retries.
    ///
    /// The inbox_action table records which actions were attempted per delivery, providing
    /// a per-run audit trail alongside the instance-wide business_action record.
    /// </summary>
    public abstract class LifeCycleWrapper {

        protected LifeCycleWrapper() { }

        // Post-construction injection — set by WorkFlowConsumerManager before dispatch.
        internal ILifeCycleExecution? _engine;
        internal IBusinessActionDAL? _businessActionDal;
        internal IInboxActionDAL? _inboxActionDal;

        // Set by AutoTransitionAsync — read by the manager after dispatch to write to outbox.next_event.
        // Safe because each event always gets a fresh wrapper instance (DI transient / Activator.CreateInstance).
        internal int? _nextEvent;

        // ── Business action helpers ────────────────────────────────────────────

        protected async Task<bool> IsBusinessActionCompletedAsync(ConsumerContext ctx, int actionCode) {
            var row = await BusinessActionDal.GetByKeyAsync(ctx.InstanceId, actionCode,
                new DbExecutionLoad(ctx.CancellationToken));
            return row?.Status == BusinessActionStatus.Completed;
        }

        protected Task<BusinessActionRecord?> GetBusinessActionAsync(ConsumerContext ctx, int actionCode)
            => BusinessActionDal.GetByKeyAsync(ctx.InstanceId, actionCode,
                new DbExecutionLoad(ctx.CancellationToken));

        protected async Task<BusinessActionExecutionResult> ExecuteBusinessActionAsync(
            ConsumerContext ctx,
            int actionCode,
            Func<CancellationToken, Task<object?>> action,
            BusinessActionExecutionMode mode = BusinessActionExecutionMode.SkipIfCompleted) {

            if (action == null) throw new ArgumentNullException(nameof(action));
            if (actionCode <= 0) throw new ArgumentOutOfRangeException(nameof(actionCode));

            var load = new DbExecutionLoad(ctx.CancellationToken);
            var existing = await BusinessActionDal.GetByKeyAsync(ctx.InstanceId, actionCode, load);

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
                ctx.InstanceId, actionCode, BusinessActionStatus.Running, load);

            if (existing != null)
                await BusinessActionDal.SetRunningAsync(actionId, load);

            // Record the attempt in inbox_action (per-delivery audit).
            await InboxActionDal.UpsertAsync(ctx.InboxId, actionId, InboxActionStatus.Attempted, null, load);

            try {
                var payload = await action(ctx.CancellationToken);
                var resultJson = ToResultJson(payload);
                await BusinessActionDal.SetCompletedAsync(actionId, resultJson, load);
                await InboxActionDal.UpsertAsync(ctx.InboxId, actionId, InboxActionStatus.Completed, null, load);
                return new BusinessActionExecutionResult {
                    ActionId = actionId,
                    Executed = true,
                    AlreadyCompleted = false,
                    ResultJson = resultJson
                };
            } catch (OperationCanceledException) when (ctx.CancellationToken.IsCancellationRequested) {
                throw;
            } catch (Exception ex) {
                var errorMsg = $"{ex.GetType().Name}: {ex.Message}";
                try {
                    await BusinessActionDal.SetFailedAsync(actionId, errorMsg, load);
                    await InboxActionDal.UpsertAsync(ctx.InboxId, actionId, InboxActionStatus.Failed, errorMsg, load);
                } catch {
                    // Best effort — original exception is more important.
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

        /// <summary>
        /// Records the policy-defined next transition to fire after this event's ACK is confirmed.
        /// Does NOT call the engine directly — the consumer manager fires the trigger after
        /// AckAsync succeeds (both the inline path and the outbox retry path).
        ///
        /// This separation is critical: the engine's ACK gate blocks new triggers on an instance
        /// while the current event's ACK is still pending. Firing after ACK confirmation avoids
        /// that conflict entirely.
        ///
        /// On trigger failure the manager fires a NEXT_EVENT_FAILED notice. The ACK is already
        /// confirmed at that point so it cannot be undone — the engine monitor will detect the
        /// stale state and raise its own alert.
        ///
        /// NOTE: has nothing to do with AckOutcome. The handler returns its ACK outcome independently.
        /// </summary>
        protected Task AutoTransitionAsync(ConsumerContext ctx, bool success) {
            var eventCode = success ? ctx.OnSuccessEvent : ctx.OnFailureEvent;
            if (eventCode == null) {
                var label = success ? "success" : "failure";
                throw new InvalidOperationException(
                    $"AutoTransitionAsync: no {label} event defined in policy for instance {ctx.InstanceGuid}. " +
                    $"Add a complete.{label} event code to the matching policy rule.");
            }
            _nextEvent = eventCode.Value;
            return Task.CompletedTask;
        }

        protected static string PickEvent(string? preferred, string fallback)
            => !string.IsNullOrWhiteSpace(preferred) ? preferred : fallback;

        // ── Unhandled event fallbacks ──────────────────────────────────────────

        protected abstract Task<AckOutcome> OnUnhandledTransitionAsync(ILifeCycleTransitionEvent evt, ConsumerContext ctx);
        protected abstract Task<AckOutcome> OnUnhandledHookAsync(ILifeCycleHookEvent evt, ConsumerContext ctx);

        // ── Internal dispatch entry points ─────────────────────────────────────

        internal async Task<AckOutcome> DispatchTransitionAsync(ILifeCycleTransitionEvent evt, ConsumerContext ctx) {
            // TransitionMode: engine-driven state advance — skip business logic entirely.
            // The engine has already run all gate/effect hooks; this call just fires the next event code.
            // ctx.DispatchMode is authoritative (stored in inbox, survives monitor re-dispatch).
            if (ctx.DispatchMode == TransitionDispatchMode.TransitionMode) {
                _nextEvent = ctx.OnSuccessEvent;
                return AckOutcome.Processed;
            }

            var cache = DispatchCacheStore.GetOrBuild(GetType());
            AckOutcome outcome;
            if (cache.Transitions.TryGetValue(evt.EventCode, out var candidates)) {
                var handler = PickBestHandler(candidates, ctx.HandlerVersion);
                outcome = handler != null
                    ? await handler(this, evt, ctx)
                    : await OnUnhandledTransitionAsync(evt, ctx);
            } else {
                outcome = await OnUnhandledTransitionAsync(evt, ctx);
            }

            // ValidationMode: hooks are in progress — engine drives the next transition via ACK pipeline.
            // Suppress auto-transition so the consumer does not fire the next event directly.
            // The ACK gate will also block any premature trigger from outside.
            if (ctx.DispatchMode == TransitionDispatchMode.ValidationMode) {
                _nextEvent = null;
            }

            return outcome;
        }

        internal Task<AckOutcome> DispatchHookAsync(ILifeCycleHookEvent evt, ConsumerContext ctx) {
            var cache = DispatchCacheStore.GetOrBuild(GetType());
            var key = evt.Route ?? string.Empty;
            if (cache.Hooks.TryGetValue(key, out var candidates)) {
                var handler = PickBestHandler(candidates, ctx.HandlerVersion);
                if (handler != null) return handler(this, evt, ctx);
            }
            return OnUnhandledHookAsync(evt, ctx);
        }

        // ── Internal helpers ───────────────────────────────────────────────────

        private IBusinessActionDAL BusinessActionDal =>
            _businessActionDal ?? throw new InvalidOperationException("LifeCycleWrapper not initialized by ConsumerManager.");

        private IInboxActionDAL InboxActionDal =>
            _inboxActionDal ?? throw new InvalidOperationException("LifeCycleWrapper not initialized by ConsumerManager.");

        /// <summary>
        /// Access the engine to trigger events, fetch timelines, upsert runtime logs, etc.
        /// Available inside handler methods dispatched by WorkFlowConsumerManager.
        /// </summary>
        protected ILifeCycleExecution Engine =>
            _engine ?? throw new InvalidOperationException("Engine was not injected by the consumer manager.");

        private static string? ToResultJson(object? value) {
            if (value == null) return null;
            if (value is string s) return s;
            return JsonSerializer.Serialize(value);
        }

        private static THandler? PickBestHandler<THandler>(List<(int MinVersion, THandler Handler)> candidates, int handlerVersion) where THandler : class {
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
