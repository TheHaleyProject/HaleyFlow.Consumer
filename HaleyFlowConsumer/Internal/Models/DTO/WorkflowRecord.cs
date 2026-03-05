using Haley.Enums;
using System;

namespace Haley.Models {
    public sealed class WorkflowRecord {
        public long Id { get; set; }
        public string AckGuid { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public WorkflowKind Kind { get; set; }
        public long ConsumerId { get; set; }
        public long DefId { get; set; }
        public long DefVersionId { get; set; }
        /// <summary>Pinned handler version for this entity. Null until first event is processed.</summary>
        public int? HandlerVersion { get; set; }
        public string? InstanceGuid { get; set; }
        public int? OnSuccess { get; set; }
        public int? OnFailure { get; set; }
        public DateTime Occurred { get; set; }
        /// <summary>Null for hook events.</summary>
        public int? EventCode { get; set; }
        /// <summary>Null for transition events.</summary>
        public string? Route { get; set; }
        public DateTime Created { get; set; }
        public HandlerUpgrade HandlerUpgrade { get; set; } = HandlerUpgrade.Pinned;
    }
}
