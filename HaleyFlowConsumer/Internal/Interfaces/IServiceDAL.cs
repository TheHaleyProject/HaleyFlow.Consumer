namespace Haley.Internal {
    /// <summary>Aggregates all consumer-side DAL sub-interfaces.</summary>
    internal interface IServiceDAL {
        /// <summary>inbox table — one row per event delivery.</summary>
        IInboxDAL Inbox { get; }
        /// <summary>inbox_status table — processing state per delivery.</summary>
        IInboxStatusDAL InboxStatus { get; }
        /// <summary>inbox_action table — per-delivery business action checkpoints.</summary>
        IInboxActionDAL InboxAction { get; }
        /// <summary>business_action table — instance-scoped action audit.</summary>
        IBusinessActionDAL BusinessAction { get; }
        /// <summary>outbox table — ACK delivery queue.</summary>
        IOutboxDAL Outbox { get; }
        /// <summary>instance table — consumer-side mirror of engine instances.</summary>
        IInstanceDAL Instance { get; }
        ITimelineDAL Timeline { get; }
    }
}
