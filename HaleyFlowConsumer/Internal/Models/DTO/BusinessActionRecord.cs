using Haley.Enums;
using System;

namespace Haley.Models {
    public sealed class BusinessActionRecord {
        public long Id { get; set; }
        public long DefId { get; set; }
        public string EntityId { get; set; } = string.Empty;
        public int ActionCode { get; set; }
        public BusinessActionStatus Status { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? ResultJson { get; set; }
    }
}
