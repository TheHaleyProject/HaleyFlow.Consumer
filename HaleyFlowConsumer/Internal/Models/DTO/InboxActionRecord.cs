using Haley.Enums;

namespace Haley.Models {
    /// <summary>
    /// Per-delivery checkpoint: records that a specific business action was attempted
    /// for a particular inbox delivery (inbox_action table).
    /// Complements the instance-scoped BusinessActionRecord.
    /// </summary>
    public sealed class InboxActionRecord {
        public long InboxId { get; set; }
        /// <summary>FK to business_action.id.</summary>
        public long ActionId { get; set; }
        /// <summary>Per-delivery status: Attempted, Completed, or Failed.</summary>
        public InboxActionStatus Status { get; set; } = InboxActionStatus.Attempted;
        public string? LastError { get; set; }
    }
}
