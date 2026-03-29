using Haley.Abstractions;
using Haley.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Haley.Services {
    /// <summary>
    /// Client-side validator for WorkflowBackfillObjects.
    ///
    /// Usage:
    ///   1. Construct with an IWorkFlowEngineAccessor (or IWorkFlowConsumerService).
    ///   2. Call ValidateAsync(obj) — fetches definition snapshot from engine (cached per name),
    ///      checks every transition in the backfill object, stamps obj.Validated on success.
    ///   3. Call engine.ImportBackfillAsync(obj) — engine accepts only pre-validated objects.
    ///
    /// Validation rules:
    ///   - Every (FromState, ToState, EventCode) triple must be a valid transition in the definition.
    ///   - Hook routes are validated with warnings only (not hard failures) because legacy systems
    ///     may not have tracked hook-level events with route-level granularity.
    ///   - EventCode (int) is used — not the event name, which can be renamed.
    /// </summary>
    public sealed class WorkflowBackfillValidator {
        private readonly IWorkFlowEngineAccessor _accessor;
        // Cache keyed by "envCode:definitionName" so 1000 objects don't cause 1000 engine round-trips.
        private readonly Dictionary<string, WorkflowDefinitionSnapshot> _snapshotCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly SemaphoreSlim _cacheLock = new(1, 1);

        public WorkflowBackfillValidator(IWorkFlowEngineAccessor accessor) {
            _accessor = accessor ?? throw new ArgumentNullException(nameof(accessor));
        }

        /// <summary>
        /// Validates the backfill object against the workflow definition.
        /// Stamps <see cref="WorkflowBackfillObject.Validated"/> on success.
        /// Returns a result indicating success and any warnings/errors.
        /// </summary>
        public async Task<BackfillValidationResult> ValidateAsync(WorkflowBackfillObject obj, CancellationToken ct = default) {
            if (obj == null) throw new ArgumentNullException(nameof(obj));

            var errors   = new List<string>();
            var warnings = new List<string>();

            if (string.IsNullOrWhiteSpace(obj.WorkflowName)) { errors.Add($"{HaleyFlowErrorCodes.BackfillMissingWorkflowName}: WorkflowName is required."); }
            if (string.IsNullOrWhiteSpace(obj.EntityRef))    { errors.Add($"{HaleyFlowErrorCodes.BackfillMissingEntityRef}: EntityRef is required."); }
            if (obj.Transitions == null || obj.Transitions.Count == 0) { errors.Add($"{HaleyFlowErrorCodes.BackfillNoTransitions}: At least one transition is required."); }

            if (errors.Count > 0) return BackfillValidationResult.Fail(errors, warnings);

            var snapshot = await GetSnapshotAsync(obj.EnvCode, obj.WorkflowName!, ct);
            if (snapshot == null) {
                errors.Add($"{HaleyFlowErrorCodes.BackfillDefinitionNotFound}: Definition not found: envCode={obj.EnvCode} name={obj.WorkflowName}");
                return BackfillValidationResult.Fail(errors, warnings);
            }

            // Build fast-lookup sets from the snapshot.
            // Key: (fromState, toState, eventCode) — eventCode is int, stable across renames.
            var validTransitions = new HashSet<(string from, string to, int eventCode)>(TransitionKeyComparer.Instance);
            var validRoutes      = new Dictionary<(string from, string to, int eventCode), HashSet<string>>(TransitionKeyComparer.Instance);

            foreach (var t in snapshot.Transitions) {
                var key = (t.FromState, t.ToState, t.EventCode);
                validTransitions.Add(key);
                var routeSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var h in t.Hooks) routeSet.Add(h.Route);
                validRoutes[key] = routeSet;
            }

            for (var i = 0; i < obj.Transitions!.Count; i++) {
                var tr  = obj.Transitions[i];
                var idx = i + 1;

                var key = (tr.FromState ?? string.Empty, tr.ToState ?? string.Empty, tr.EventCode);

                if (!validTransitions.Contains(key)) {
                    errors.Add($"{HaleyFlowErrorCodes.BackfillInvalidTransition}: Transition #{idx}: ({tr.FromState} → {tr.ToState} via event code {tr.EventCode}) is not a valid transition in definition '{obj.WorkflowName}'.");
                    continue;
                }

                // Hook validation — warnings only.
                if (tr.Hooks != null && validRoutes.TryGetValue(key, out var knownRoutes)) {
                    foreach (var bh in tr.Hooks) {
                        if (string.IsNullOrWhiteSpace(bh.Route)) {
                            warnings.Add($"{HaleyFlowErrorCodes.BackfillEmptyHookRoute}: Transition #{idx}: a hook entry has an empty Route — skipped.");
                            continue;
                        }
                        if (!knownRoutes.Contains(bh.Route)) {
                            warnings.Add($"{HaleyFlowErrorCodes.BackfillUnknownHookRoute}: Transition #{idx}: hook route '{bh.Route}' is not defined in the policy for this transition — will be recorded as-is.");
                        }
                    }
                }
            }

            if (errors.Count > 0) return BackfillValidationResult.Fail(errors, warnings);

            obj.MarkValidated();
            return BackfillValidationResult.Success(warnings);
        }

        private async Task<WorkflowDefinitionSnapshot?> GetSnapshotAsync(int envCode, string defName, CancellationToken ct) {
            var cacheKey = $"{envCode}:{defName}";

            await _cacheLock.WaitAsync(ct);
            try {
                if (_snapshotCache.TryGetValue(cacheKey, out var cached)) return cached;
            } finally {
                _cacheLock.Release();
            }

            var engine   = await _accessor.GetEngineAsync(ct);
            var snapshot = await engine.GetDefinitionSnapshotAsync(envCode, defName, ct);
            if (snapshot == null) return null;

            await _cacheLock.WaitAsync(ct);
            try { _snapshotCache[cacheKey] = snapshot; }
            finally { _cacheLock.Release(); }

            return snapshot;
        }

        /// <summary>Clears the in-memory definition cache. Call after importing a new definition version.</summary>
        public void ClearCache() {
            _cacheLock.Wait();
            try { _snapshotCache.Clear(); }
            finally { _cacheLock.Release(); }
        }
    }

    // Helper: comparer for (string from, string to, int eventCode) tuples.
    // String parts are case-insensitive; int eventCode uses value equality.
    //file sealed.. so this class cannot be inherited, which is fine since it's just a comparer implementation detail.
    file sealed class TransitionKeyComparer : IEqualityComparer<(string from, string to, int eventCode)> {
        public static readonly TransitionKeyComparer Instance = new();

        public bool Equals((string from, string to, int eventCode) x, (string from, string to, int eventCode) y)
            => StringComparer.OrdinalIgnoreCase.Equals(x.from, y.from)
            && StringComparer.OrdinalIgnoreCase.Equals(x.to,   y.to)
            && x.eventCode == y.eventCode;

        public int GetHashCode((string from, string to, int eventCode) obj)
            => HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.from),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.to),
                obj.eventCode);
    }
}
