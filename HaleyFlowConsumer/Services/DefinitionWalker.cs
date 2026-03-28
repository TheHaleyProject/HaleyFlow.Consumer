using Haley.Abstractions;
using Haley.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Haley.Services {
    /// <summary>
    /// Walks a workflow definition snapshot and assembles a <see cref="WorkflowBackfillObject"/>
    /// by calling consumer-provided callbacks at each transition node.
    ///
    /// Haley owns graph traversal. The consumer owns domain data via <see cref="IBackfillDataProvider"/>.
    ///
    /// Usage:
    ///   var walker = new DefinitionWalker(accessor);
    ///   var obj = await walker.WalkAsync(snapshot, entityRef, provider, ct);
    ///   await engine.ImportBackfillAsync(obj, ct);
    /// </summary>
    public sealed class DefinitionWalker {
        private readonly WorkflowBackfillValidator _validator;

        public DefinitionWalker(IWorkFlowEngineAccessor accessor) {
            if (accessor == null) throw new ArgumentNullException(nameof(accessor));
            _validator = new WorkflowBackfillValidator(accessor);
        }

        /// <summary>
        /// Walks the snapshot transition graph in definition order, calling the provider at each node.
        /// Stops walking when the provider returns null for a transition (entity did not reach that event).
        /// Returns a validated <see cref="WorkflowBackfillObject"/> ready for ImportBackfillAsync.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if validation fails after assembly.</exception>
        public async Task<WorkflowBackfillObject> WalkAsync(WorkflowDefinitionSnapshot snapshot, string entityRef, IBackfillDataProvider provider, CancellationToken ct = default) {

            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (string.IsNullOrWhiteSpace(entityRef)) throw new ArgumentException("entityRef is required.", nameof(entityRef));
            if (provider  == null) throw new ArgumentNullException(nameof(provider));

            var obj = new WorkflowBackfillObject {
                WorkflowName = snapshot.DefinitionName,
                EnvCode      = snapshot.EnvCode,
                EntityRef    = entityRef,
            };

            foreach (var t in snapshot.Transitions) {
                var data = await provider.GetTransitionDataAsync(t.EventCode, ct);
                if (data == null) break; // entity did not reach this event — stop here

                var transition = new BackfillTransition {
                    FromState = t.FromState,
                    ToState   = t.ToState,
                    EventCode = t.EventCode,
                    Timestamp = data.Timestamp,
                    Actor     = data.Actor,
                    Payload   = data.Payload,
                };

                foreach (var h in t.Hooks) {
                    var hookData = await provider.GetHookDataAsync(t.EventCode, h.Route, ct);
                    if (hookData == null) continue; // not tracked in legacy system — skip (warning from validator)

                    transition.Hooks.Add(new BackfillHook {
                        Route     = h.Route,
                        Timestamp = hookData.Timestamp,
                        Outcome   = hookData.Outcome,
                    });
                }

                obj.Transitions.Add(transition);
            }

            var result = await _validator.ValidateAsync(obj, ct);
            if (!result.IsValid)
                throw new InvalidOperationException(
                    $"Backfill assembly failed for entity '{entityRef}': {string.Join("; ", result.Errors)}");

            return obj;
        }
    }
}
