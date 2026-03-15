using Haley.Abstractions;
using Haley.Utils;

namespace Haley.Internal {
    internal sealed class MariaConsumerServiceDAL : DALUtilBase, IConsumerServiceDAL {
        public IConsumerInboxDAL Workflow { get; }
        public IConsumerBusinessActionDAL BusinessAction { get; }
        public IConsumerInboxStatusDAL Inbox { get; }
        public IConsumerInboxStepDAL InboxStep { get; }
        public IConsumerOutboxDAL Outbox { get; }
        public IConsumerEntityDAL Entity { get; }
        public IConsumerWorkflowDAL EntityWorkflow { get; }
        public IConsumerTimelineDAL Timeline { get; }

        public MariaConsumerServiceDAL(IAdapterGateway agw, string key) : base (agw,key) {
            Workflow = new MariaConsumerInboxDAL(this);
            BusinessAction = new MariaConsumerBusinessActionDAL(this);
            Inbox = new MariaConsumerInboxStatusDAL(this);
            InboxStep = new MariaConsumerInboxStepDAL(this);
            Outbox = new MariaConsumerOutboxDAL(this);
            Entity = new MariaConsumerEntityDAL(this);
            EntityWorkflow = new MariaConsumerWorkflowDAL(this);
            Timeline = new MariaConsumerTimelineDAL(this);
        }
    }
}
