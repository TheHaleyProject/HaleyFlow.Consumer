namespace Haley.Models {
    /// <summary>
    /// Consumer-side record that maps an entity to a workflow definition.
    /// One entity can participate in multiple workflow definitions.
    /// </summary>
    public sealed class EntityWorkflowRecord {
        public long Id { get; set; }
        /// <summary>FK to entity.id — the stable business-entity UUID.</summary>
        public string Entity { get; set; } = string.Empty;
        /// <summary>Workflow definition name (matches the engine's def_name).</summary>
        public string DefName { get; set; } = string.Empty;
        /// <summary>Engine-assigned instance GUID. Empty until the workflow is triggered.</summary>
        public string InstanceId { get; set; } = string.Empty;
        /// <summary>True once TriggerAsync has been called and the engine accepted the trigger.</summary>
        public bool IsTriggered { get; set; }
    }
}
