using Haley.Enums;
using Haley.Models;
using Haley.Utils;
using System.Linq.Expressions;
using System.Reflection;
using Haley.Internal;

namespace Haley.Abstractions {

    /// <summary>
    /// Base class for all workflow event handlers. Derive from this class and decorate methods with
    /// <see cref="TransitionHandlerAttribute"/> or <see cref="HookHandlerAttribute"/>.
    /// Resolved from DI per dispatch — constructor-inject your own dependencies normally.
    /// </summary>
    public abstract class LifeCycleWrapper {

        // Set by ConsumerService before dispatch — not for user consumption.
        internal IConsumerInboxStepDAL? _stepDal;

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
            return row != null && row.GetInt("status") == (int)InboxStepStatus.Completed;
        }

        //Dont wrap the unhandled ones in virtual method.. By default it should always be handled by the consumers.. Dont assume and makr as processed.
        protected abstract Task<AckOutcome> OnUnhandledTransitionAsync(ILifeCycleTransitionEvent evt, ConsumerContext ctx);
        protected abstract Task<AckOutcome> OnUnhandledHookAsync(ILifeCycleHookEvent evt, ConsumerContext ctx);

       internal Task<AckOutcome> DispatchTransitionAsync(ILifeCycleTransitionEvent evt, ConsumerContext ctx) {
            var cache = DispatchCacheStore.GetOrBuild(GetType());
            if (cache.Transitions.TryGetValue(evt.EventCode, out var candidates)) {
                var handler = PickBestHandler(candidates, ctx.HandlerVersion);
                if (handler != null) return handler(this, evt, ctx);
            }
            return OnUnhandledTransitionAsync(evt, ctx);
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

        private IConsumerInboxStepDAL StepDal =>
            _stepDal ?? throw new InvalidOperationException("LifeCycleWrapper not initialized by ConsumerService.");

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
