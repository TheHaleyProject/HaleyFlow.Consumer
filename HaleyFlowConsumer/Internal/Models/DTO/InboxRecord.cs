using Haley.Enums;
using System;

namespace Haley.Models {
    public sealed class InboxRecord {
        public long WfId { get; set; }
        public string? PayloadJson { get; set; }
        public string? ParamsJson { get; set; }
        public DateTime ReceivedAt { get; set; }
        public DateTime Modified { get; set; }
        public InboxStatus Status { get; set; } = InboxStatus.Received;
        public int AttemptCount { get; set; }
        public string? LastError { get; set; }
    }
}
