using Haley.Abstractions;
using Haley.Enums;
using Haley.Models;
using System.Linq.Expressions;
using System.Reflection;

namespace Haley.Internal {

    internal static class DispatchCacheStore {
        private static readonly Dictionary<Type, HandlerDispatchCache> _caches = new(); //one wrapper can have both transition and hook handlers.. so we dont' want two dictionaries.. clean way is to have one dictionary and a class containing the child diectionaries.
        private static readonly object _lock = new();

        internal static HandlerDispatchCache GetOrBuild(Type wrapperType) {
            lock (_lock) {
                if (_caches.TryGetValue(wrapperType, out var existing)) return existing;
                var cache = BuildCache(wrapperType);
                _caches[wrapperType] = cache;
                return cache;
            }
        }

        private static HandlerDispatchCache BuildCache(Type wrapperType) {
            var transitions = new Dictionary<int, List<(int, Func<LifeCycleWrapper, ILifeCycleTransitionEvent, ConsumerContext, Task<AckOutcome>>)>>();
            var hooks = new Dictionary<string, List<(int, Func<LifeCycleWrapper, ILifeCycleHookEvent, ConsumerContext, Task<AckOutcome>>)>>(StringComparer.OrdinalIgnoreCase);

            foreach (var method in wrapperType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) {
                //Fetch all Transaction handlers
                foreach (var attr in method.GetCustomAttributes<TransitionHandlerAttribute>(inherit: true)) {
                    var del = CompileTransitionDelegate(wrapperType, method);
                    if (!transitions.TryGetValue(attr.EventCode, out var list)) {
                        list = new List<(int, Func<LifeCycleWrapper, ILifeCycleTransitionEvent, ConsumerContext, Task<AckOutcome>>)>();
                        transitions[attr.EventCode] = list;
                    }
                    list.Add((attr.MinVersion, del));
                }

                //Fetch all hook handlers
                foreach (var attr in method.GetCustomAttributes<HookHandlerAttribute>(inherit: true)) {
                    var del = CompileHookDelegate(wrapperType, method);
                    if (!hooks.TryGetValue(attr.Route, out var list)) {
                        list = new List<(int, Func<LifeCycleWrapper, ILifeCycleHookEvent, ConsumerContext, Task<AckOutcome>>)>();
                        hooks[attr.Route] = list;
                    }
                    list.Add((attr.MinVersion, del));
                }
            }

            return new HandlerDispatchCache { Transitions = transitions, Hooks = hooks };
        }

        private static Func<LifeCycleWrapper, ILifeCycleTransitionEvent, ConsumerContext, Task<AckOutcome>> CompileTransitionDelegate(Type wrapperType, MethodInfo method) {
            var wParam = Expression.Parameter(typeof(LifeCycleWrapper), "w");
            var eParam = Expression.Parameter(typeof(ILifeCycleTransitionEvent), "e");
            var cParam = Expression.Parameter(typeof(ConsumerContext), "c");
            var castW = Expression.Convert(wParam, wrapperType);
            var call = Expression.Call(castW, method, eParam, cParam);
            // If method returns Task (not Task<AckOutcome>), wrap it
            if (method.ReturnType == typeof(Task)) {
                var innerLambda = Expression.Lambda<Func<LifeCycleWrapper, ILifeCycleTransitionEvent, ConsumerContext, Task>>(call, wParam, eParam, cParam).Compile();
                return (w, e, c) => WrapTaskTransition(innerLambda(w, e, c));
            }
            return Expression.Lambda<Func<LifeCycleWrapper, ILifeCycleTransitionEvent, ConsumerContext, Task<AckOutcome>>>(call, wParam, eParam, cParam).Compile();
        }

        private static Func<LifeCycleWrapper, ILifeCycleHookEvent, ConsumerContext, Task<AckOutcome>>
            CompileHookDelegate(Type wrapperType, MethodInfo method) {
            var wParam = Expression.Parameter(typeof(LifeCycleWrapper), "w");
            var eParam = Expression.Parameter(typeof(ILifeCycleHookEvent), "e");
            var cParam = Expression.Parameter(typeof(ConsumerContext), "c");
            var castW = Expression.Convert(wParam, wrapperType);
            var call = Expression.Call(castW, method, eParam, cParam);
            if (method.ReturnType == typeof(Task)) {
                var innerLambda = Expression.Lambda<Func<LifeCycleWrapper, ILifeCycleHookEvent, ConsumerContext, Task>>(call, wParam, eParam, cParam).Compile();
                return (w, e, c) => WrapTaskHook(innerLambda(w, e, c));
            }
            return Expression.Lambda<Func<LifeCycleWrapper, ILifeCycleHookEvent, ConsumerContext, Task<AckOutcome>>>(call, wParam, eParam, cParam).Compile();
        }

        private static async Task<AckOutcome> WrapTaskTransition(Task t) { await t; return AckOutcome.Processed; }
        private static async Task<AckOutcome> WrapTaskHook(Task t) { await t; return AckOutcome.Processed; }
    }
}
