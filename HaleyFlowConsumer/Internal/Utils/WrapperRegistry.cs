using Haley.Abstractions;
using Haley.Enums;
using System.Reflection;

namespace Haley.Internal {
    /// <summary>
    /// Holds registered wrapper types keyed by def_id (for dispatch) and by definition name
    /// (for auto-discovery). Call <see cref="RegisterAssembly"/> to populate the name index,
    /// then <see cref="Resolve"/> to bind each name to its engine-assigned def_id.
    /// </summary>
    internal sealed class WrapperRegistry {
        private readonly Dictionary<long, WrapperRegistration> _byDefId = new();
        private readonly Dictionary<string, Type> _byName = new(StringComparer.OrdinalIgnoreCase);

        // ── Manual registration (defId known at call time) ─────────────────────

        public void Register<T>(long defId) where T : LifeCycleWrapper
            => Register(defId, typeof(T));

        public void Register(long defId, Type wrapperType) {
            if (!typeof(LifeCycleWrapper).IsAssignableFrom(wrapperType))
                throw new ArgumentException($"{wrapperType.Name} must inherit LifeCycleWrapper.");
            _byDefId[defId] = new WrapperRegistration { DefId = defId, WrapperType = wrapperType };
        }

        // ── Assembly scan (name-based — defId resolved later via engine) ───────

        public void RegisterAssembly(Assembly assembly) {
            foreach (var type in assembly.GetTypes()) {
                if (type.IsAbstract || !typeof(LifeCycleWrapper).IsAssignableFrom(type)) continue;
                var attr = type.GetCustomAttribute<LifeCycleDefinitionAttribute>();
                if (attr == null) continue;
                _byName[attr.Name] = type;
            }
        }

        /// <summary>
        /// Returns all definition names discovered by <see cref="RegisterAssembly"/> that
        /// have not yet been resolved to a def_id.
        /// </summary>
        public IReadOnlyList<string> GetPendingNames() {
            var pending = new List<string>();
            foreach (var name in _byName.Keys) {
                if (!_byDefId.Values.Any(r => r.WrapperType == _byName[name]))
                    pending.Add(name);
            }
            return pending;
        }

        /// <summary>
        /// Binds a discovered definition name to its engine-assigned def_id.
        /// Called during startup after querying the engine.
        /// </summary>
        public void Resolve(string name, long defId) {
            if (_byName.TryGetValue(name, out var wrapperType))
                Register(defId, wrapperType);
        }

        // ── Dispatch ────────────────────────────────────────────────────────────

        public bool TryGetRegistration(long defId, out WrapperRegistration? registration)
            => _byDefId.TryGetValue(defId, out registration);

        public int ResolveHandlerVersion(long defId, int storedVersion, HandlerUpgrade upgrade) {
            if (upgrade == HandlerUpgrade.Pinned || !_byDefId.TryGetValue(defId, out var reg))
                return storedVersion;
            return Math.Max(storedVersion, reg.MaxDeclaredVersion);
        }
    }
}
