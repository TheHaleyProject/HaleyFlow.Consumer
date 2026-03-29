using Haley.Enums;
using System;

namespace Haley.Models {
    public sealed class InboxRecord {
        public long Id { get; set; }
        public string AckGuid { get; set; } = string.Empty;
        public WorkflowKind Kind { get; set; }
        /// <summary>FK to instance.id — links this inbox delivery to its workflow instance.</summary>
        public long InstanceId { get; set; }
        /// <summary>Pinned handler version for this instance. Null until first event is processed.</summary>
        public int? HandlerVersion { get; set; }
        public int? OnSuccess { get; set; }
        public int? OnFailure { get; set; }
        public DateTime Occurred { get; set; }
        /// <summary>Null for hook and complete events.</summary>
        public int? EventCode { get; set; }
        /// <summary>Null for transition and complete events.</summary>
        public string? Route { get; set; }
        /// <summary>How many times this hook has been dispatched (including this delivery). Always 1 for transition and complete events.</summary>
        public int RunCount { get; set; } = 1;
        public DateTime Created { get; set; }
        public HandlerUpgrade HandlerUpgrade { get; set; } = HandlerUpgrade.Pinned;
        /// <summary>Dispatch mode for transition events. Ignored for hook and complete rows.</summary>
        public TransitionDispatchMode DispatchMode { get; set; } = TransitionDispatchMode.NormalRun;
        /// <summary>Gate or Effect. Null for transition and complete rows.</summary>
        public HookType? HookType { get; set; }
    }
}
