using Haley.Abstractions;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Haley.Services {
    /// <summary>
    /// Default relay state store for zero-infrastructure local execution.
    /// Replace IWorkflowRelayStateStore in the host application for durable, multi-day human waits.
    /// </summary>
    public sealed class InMemoryWorkflowRelayStateStore : IWorkflowRelayStateStore {
        private readonly ConcurrentDictionary<string, string> _states = new(StringComparer.OrdinalIgnoreCase);

        public Task<string?> GetCurrentStateAsync(string workflowName, string entityId, int envCode = 0, CancellationToken ct = default) {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(_states.TryGetValue(BuildKey(workflowName, entityId, envCode), out var state) ? state : null);
        }

        public Task SaveCurrentStateAsync(string workflowName, string entityId, int envCode, string currentState, CancellationToken ct = default) {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(currentState)) return Task.CompletedTask;

            _states[BuildKey(workflowName, entityId, envCode)] = currentState.Trim();
            return Task.CompletedTask;
        }

        private static string BuildKey(string workflowName, string entityId, int envCode)
            => $"{envCode}:{Normalize(workflowName)}:{Normalize(entityId)}";

        private static string Normalize(string value) => (value ?? string.Empty).Trim().ToLowerInvariant();
    }
}
