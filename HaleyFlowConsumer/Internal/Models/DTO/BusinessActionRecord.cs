using Haley.Enums;
using System;

namespace Haley.Models {
    public sealed class BusinessActionRecord {
        public long Id { get; set; }
        /// <summary>FK to instance.id — scopes this action to a specific workflow instance.</summary>
        public long InstanceId { get; set; }
        public int ActionCode { get; set; }
        public BusinessActionStatus Status { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? ResultJson { get; set; }
        public string? LastError { get; set; }
    }
}
