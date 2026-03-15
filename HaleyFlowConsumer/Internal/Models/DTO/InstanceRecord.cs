namespace Haley.Models {
    /// <summary>
    /// Consumer-side mirror of the engine's workflow instance.
    /// One row per engine instance GUID.
    /// </summary>
    public sealed class InstanceRecord {
        public long Id { get; set; }
        /// <summary>Instance GUID from the engine — the stable cross-system reference.</summary>
        public string Guid { get; set; } = string.Empty;
        /// <summary>Workflow definition name (matches the engine's def_name).</summary>
        public string DefName { get; set; } = string.Empty;
        /// <summary>Actual workflow definition version number (v1, v2, v3...) from the engine.</summary>
        public int DefVersion { get; set; }
        /// <summary>Entity GUID — the business entity this instance belongs to.</summary>
        public string EntityGuid { get; set; } = string.Empty;
        /// <summary>Engine-side instance creation time mirrored into the consumer database.</summary>
        public DateTime Created { get; set; }
    }
}
