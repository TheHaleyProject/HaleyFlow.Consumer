using Haley.Enums;
using System;

namespace Haley.Models {
    public sealed class InboxStepRecord {
        public long Id { get; set; }
        public long InboxId { get; set; }
        public int StepCode { get; set; }
        public InboxStepStatus Status { get; set; } = InboxStepStatus.Pending;
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? ResultJson { get; set; }
        public string? LastError { get; set; }
    }
}
