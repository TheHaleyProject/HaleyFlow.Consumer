using Haley.Abstractions;
using Haley.Enums;

namespace Haley.Internal {
    /// <summary>
    /// Holds registered wrapper types keyed by def_id.
    /// Resolves the correct wrapper instance from DI and computes effective handler version.
    /// </summary>
    internal sealed class WrapperRegistry {
        private readonly Dictionary<long, WrapperRegistration> _byDefId = new();

        public void Register<T>(long defId) where T : LifeCycleWrapper
            => Register(defId, typeof(T));

        public void Register(long defId, Type wrapperType) {
            if (!typeof(LifeCycleWrapper).IsAssignableFrom(wrapperType)) throw new ArgumentException($"{wrapperType.Name} must inherit LifeCycleWrapper.");
            _byDefId[defId] = new WrapperRegistration { DefId = defId, WrapperType = wrapperType };
        }

        public bool TryGetRegistration(long defId, out WrapperRegistration? registration)
            => _byDefId.TryGetValue(defId, out registration);

        /// <summary>
        /// Resolves the effective handler version for a dispatch.
        /// Pinned: use stored handler_version.
        /// AllowUpgrade: use max MinVersion across all decorated methods (if higher).
        /// </summary>
        public int ResolveHandlerVersion(long defId, int storedVersion, HandlerUpgrade upgrade) {
            if (upgrade == HandlerUpgrade.Pinned || !_byDefId.TryGetValue(defId, out var reg))
                return storedVersion;
            return Math.Max(storedVersion, reg.MaxDeclaredVersion);
        }
    }
}
