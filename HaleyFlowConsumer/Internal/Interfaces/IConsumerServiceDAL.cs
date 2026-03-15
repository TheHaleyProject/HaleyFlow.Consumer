namespace Haley.Internal {
    /// <summary>Aggregates all consumer-side DAL sub-interfaces.</summary>
    internal interface IConsumerServiceDAL {
        IConsumerInboxDAL Workflow { get; }
        IConsumerBusinessActionDAL BusinessAction { get; }
        IConsumerInboxStatusDAL Inbox { get; }
        IConsumerInboxStepDAL InboxStep { get; }
        IConsumerOutboxDAL Outbox { get; }
        IConsumerEntityDAL Entity { get; }
        IConsumerWorkflowDAL EntityWorkflow { get; }
        IConsumerTimelineDAL Timeline { get; }
    }
}
