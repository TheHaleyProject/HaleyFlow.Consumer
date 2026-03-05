using Haley.Abstractions;
using Haley.Enums;
using Haley.Models;

namespace Haley.Internal {
    internal sealed class HandlerDispatchCache {

        // we prepare this, so that during runtime, there is not reflection overhead.
        internal Dictionary<int, List<(int MinVersion, Func<LifeCycleWrapper, ILifeCycleTransitionEvent, ConsumerContext, Task<AckOutcome>> Handler)>> Transitions { get; init; } = new(); //with event code.. 
        internal Dictionary<string, List<(int MinVersion, Func<LifeCycleWrapper, ILifeCycleHookEvent, ConsumerContext, Task<AckOutcome>> Handler)>> Hooks { get; init; } = new(StringComparer.OrdinalIgnoreCase); //hook only has route name.. so string.
    }
}
