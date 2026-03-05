using Haley.Abstractions;

namespace Haley.Internal {
    internal sealed class MariaConsumerDAL : IConsumerDAL {
        public IConsumerWorkflowDAL Workflow { get; }
        public IConsumerInboxDAL Inbox { get; }
        public IConsumerInboxStepDAL InboxStep { get; }
        public IConsumerOutboxDAL Outbox { get; }

        public MariaConsumerDAL(IDALUtilBase db) {
            Workflow = new MariaConsumerWorkflowDAL(db);
            Inbox = new MariaConsumerInboxDAL(db);
            InboxStep = new MariaConsumerInboxStepDAL(db);
            Outbox = new MariaConsumerOutboxDAL(db);
        }
    }
}
