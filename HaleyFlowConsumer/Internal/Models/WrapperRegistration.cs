namespace Haley.Internal {
    internal sealed class WrapperRegistration {
        public long DefId { get; init; }
        public string DefinitionName { get; init; } = string.Empty;
        public Type WrapperType { get; init; } = null!;
        private int? _maxVersion;

        /// <summary>Highest MinVersion declared across all handler attributes on this wrapper type.</summary>
        public int MaxDeclaredVersion {
            get {
                if (_maxVersion.HasValue) return _maxVersion.Value;
                var cache = DispatchCacheStore.GetOrBuild(WrapperType);
                int max = 0;
                foreach (var candidates in cache.Transitions.Values)
                    foreach (var (mv, _) in candidates)
                        if (mv > max) max = mv;
                foreach (var candidates in cache.Hooks.Values)
                    foreach (var (mv, _) in candidates)
                        if (mv > max) max = mv;
                _maxVersion = max;
                return max;
            }
        }
    }
}
