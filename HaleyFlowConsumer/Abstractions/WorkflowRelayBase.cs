using Haley.Abstractions;
using Haley.Models;
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Haley.Abstractions {
    /// <summary>
    /// Base class for in-process workflow relay handlers.
    /// One subclass per workflow definition — mirrors the LifeCycleWrapper pattern.
    ///
    /// Subclass responsibilities:
    ///   1. Decorate with [LifeCycleDefinition("my-workflow")] — DefinitionName is read from it automatically.
    ///   2. Provide DefinitionJson (and optionally PolicyJson) — loaded at startup.
    ///   3. Override ConfigureRelay to register On/OnHook handlers on the relay.
    ///
    /// Handlers return Task&lt;bool&gt; — true = success, false = failure.
    /// The relay reads policy complete codes to decide the next event automatically.
    /// Business logic has no sequence knowledge.
    /// </summary>
    public abstract class WorkflowRelayBase {
        private WorkflowRelay? _relay;
        private string? _definitionName;

        /// <summary>
        /// Definition name — read from [LifeCycleDefinition] attribute on the subclass.
        /// Must match the workflow definition name used in FlowInitiateRequest.WorkflowName.
        /// </summary>
        public string DefinitionName {
            get {
                if (_definitionName != null) return _definitionName;
                var attr = GetType().GetCustomAttribute<LifeCycleDefinitionAttribute>();
                if (attr == null) throw new InvalidOperationException($"{GetType().Name} must be decorated with [LifeCycleDefinition(\"definition-name\")].");
                _definitionName = attr.Name;
                return _definitionName;
            }
        }

        /// <summary>Definition JSON content (states, events, transitions).</summary>
        protected abstract string DefinitionJson { get; }

        /// <summary>Policy JSON content (rules, hooks, complete codes). Return null if not applicable.</summary>
        protected virtual string? PolicyJson => null;

        /// <summary>EnvCode this relay handles. 0 = any.</summary>
        protected virtual int EnvCode => 0;

        /// <summary>Register On/OnHook handlers on the relay. Called once at startup.</summary>
        protected abstract void ConfigureRelay(WorkflowRelay relay);

        internal void Initialize() {
            var relay = WorkflowRelay.FromJson(DefinitionJson, PolicyJson, EnvCode);
            ConfigureRelay(relay);
            _relay = relay;
        }

        internal WorkflowRelay Relay => _relay ?? throw new InvalidOperationException($"WorkflowRelayBase '{DefinitionName}' has not been initialized. Ensure the hosted service has started.");
    }
}
