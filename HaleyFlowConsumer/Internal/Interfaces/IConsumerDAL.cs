namespace Haley.Internal {
    /// <summary>Aggregates all consumer-side DAL sub-interfaces.</summary>
    public interface IConsumerDAL {
        IConsumerWorkflowDAL Workflow { get; }
        IConsumerInboxDAL Inbox { get; }
        IConsumerInboxStepDAL InboxStep { get; }
        IConsumerOutboxDAL Outbox { get; }
    }
}
