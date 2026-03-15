using Haley.Abstractions;
using Haley.Enums;
using System.Reflection;

namespace Haley.Internal {
    /// <summary>
    /// The WrapperRegistry is the consumer's internal phone book — it maps engine-assigned
    /// definition IDs (def_ids) to the concrete <see cref="LifeCycleWrapper"/> subclass that
    /// handles events for that workflow definition.
    ///
    /// WHY TWO INDICES?
    /// There are two completely different moments in time when a wrapper can be "registered":
    ///
    ///   1. At startup via assembly scan (<see cref="RegisterAssembly"/>):
    ///      The developer decorates their wrapper class with [LifeCycleDefinition("loan-approval")].
    ///      At this point we know the *name* of the definition, but we don't know the *def_id*
    ///      that the engine has assigned — that id lives in the database and is only available
    ///      after we query the engine. So we store the name → Type mapping in <c>_byName</c>
    ///      as a temporary holding area.
    ///
    ///   2. At startup resolution (<see cref="Resolve"/>):
    ///      After the consumer service starts up it calls <see cref="GetPendingNames"/> to
    ///      find all names that were discovered but not yet resolved. It then calls
    ///      <c>engine.GetDefinitionIdAsync(envCode, name)</c> for each one. Once it has the
    ///      def_id it calls <see cref="Resolve"/> which moves the registration from the name
    ///      index into <c>_byDefId</c>, ready for dispatch.
    ///
    ///   3. Manual registration (<see cref="Register"/>):
    ///      If the developer already knows the def_id (e.g. it's a compile-time constant or
    ///      retrieved from config), they can skip the name-based path and register directly.
    ///      This puts the wrapper straight into <c>_byDefId</c>.
    ///
    /// DISPATCH FLOW:
    /// When an event arrives, the consumer service has the def_id in hand (from the event
    /// payload). It calls <see cref="TryGetRegistration"/> with that def_id. If found, it
    /// activates the wrapper type from DI and calls Dispatch on it. If not found, the event
    /// is quietly skipped (this consumer doesn't handle this definition).
    ///
    /// THREAD SAFETY:
    /// The registry is populated during startup (single-threaded) and then read-only during
    /// dispatch (concurrent). No locking is required because the write phase completes before
    /// the read phase begins.
    /// </summary>
    internal sealed class WrapperRegistry {
        // Primary dispatch index: def_id → registration. Populated by Register() (direct) or
        // Resolve() (after name→id lookup at startup). Read by TryGetRegistration() on every
        // incoming event.
        private readonly Dictionary<long, WrapperRegistration> _byDefId = new();

        // Temporary name index: definition name → wrapper Type. Populated by RegisterAssembly()
        // during the assembly scan phase. Entries are "promoted" to _byDefId by Resolve().
        // Case-insensitive so "loan-approval" == "Loan-Approval" == "LOAN-APPROVAL".
        private readonly Dictionary<string, Type> _byName = new(StringComparer.OrdinalIgnoreCase);

        // ── Manual registration (defId known at call time) ─────────────────────

        public void Register<T>(long defId) where T : LifeCycleWrapper
            => Register(defId, typeof(T));

        public void Register(long defId, Type wrapperType, string definitionName = "") {
            if (!typeof(LifeCycleWrapper).IsAssignableFrom(wrapperType))
                throw new ArgumentException($"{wrapperType.Name} must inherit LifeCycleWrapper.");
            _byDefId[defId] = new WrapperRegistration { DefId = defId, WrapperType = wrapperType, DefinitionName = definitionName };
        }

        // ── Assembly scan (name-based — defId resolved later via engine) ───────
        // Walk every public type in the assembly looking for concrete LifeCycleWrapper
        // subclasses that carry [LifeCycleDefinition]. Abstract types are skipped because
        // they can't be activated — they are typically intermediate base classes the
        // developer uses to share code between multiple concrete wrappers.
        // The attr.Name is the human-readable definition name that was used when the
        // workflow definition was imported into the engine DB. We store it case-insensitively
        // because developers sometimes use different casing in code vs JSON.

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
        ///
        /// Called at startup by WorkFlowConsumerService after the assembly scan is complete.
        /// The service iterates this list, calls GetDefinitionIdAsync on the engine for each
        /// name, then calls Resolve() with the returned id. Names for which the engine has
        /// no matching definition (typo, wrong environment, etc.) will remain pending but are
        /// harmless — they just won't receive events.
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
        ///
        /// If the name was never discovered by RegisterAssembly (e.g. the engine returned an
        /// id for a definition that no wrapper handles), this is a no-op — we silently ignore
        /// it because the consumer simply doesn't have a handler for that definition.
        /// </summary>
        public void Resolve(string name, long defId) {
            if (_byName.TryGetValue(name, out var wrapperType))
                Register(defId, wrapperType, name);
        }

        // ── Dispatch ────────────────────────────────────────────────────────────

        /// <summary>
        /// Looks up the wrapper registration for a given def_id. Returns false if this
        /// consumer has no handler for that definition — the caller (ConsumerService) will
        /// skip the event. This is normal: a single consumer process is rarely responsible
        /// for every definition in an environment.
        /// </summary>
        public bool TryGetRegistration(long defId, out WrapperRegistration? registration)
            => _byDefId.TryGetValue(defId, out registration);

        /// <summary>
        /// Determines which handler version the wrapper should use for a given event.
        ///
        /// Handler versioning lets developers ship new event handling logic without breaking
        /// in-flight workflow instances that were created under an older definition version.
        /// A handler method decorated with [TransitionHandler(minVersion: 2)] only applies
        /// to instances whose handler version is >= 2.
        ///
        ///  - <see cref="HandlerUpgrade.Pinned"/>: always use the version stored in the event
        ///    (the one recorded at instance creation time). Safe — old instances always get
        ///    the old logic. Useful when breaking changes make the new handler incompatible.
        ///
        ///  - <see cref="HandlerUpgrade.Latest"/> (or any non-pinned mode): take the higher
        ///    of the stored version and the wrapper's declared max version. This lets new code
        ///    "upgrade" old instances to the latest handler as soon as they are processed.
        ///    Useful for bug fixes that should apply retroactively to all in-flight instances.
        /// </summary>
        public int ResolveHandlerVersion(long defId, int storedVersion, HandlerUpgrade upgrade) {
            if (upgrade == HandlerUpgrade.Pinned || !_byDefId.TryGetValue(defId, out var reg))
                return storedVersion;
            return Math.Max(storedVersion, reg.MaxDeclaredVersion);
        }
    }
}
