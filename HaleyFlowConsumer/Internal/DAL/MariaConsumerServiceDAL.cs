using Haley.Abstractions;
using Haley.Utils;

namespace Haley.Internal {
    internal sealed class MariaConsumerServiceDAL : DALUtilBase, IConsumerServiceDAL {
        public IConsumerWorkflowDAL Workflow { get; }
        public IConsumerInboxDAL Inbox { get; }
        public IConsumerInboxStepDAL InboxStep { get; }
        public IConsumerOutboxDAL Outbox { get; }

        public MariaConsumerServiceDAL(IAdapterGateway agw, string key) : base (agw,key) {
            Workflow = new MariaConsumerWorkflowDAL(this);
            Inbox = new MariaConsumerInboxDAL(this);
            InboxStep = new MariaConsumerInboxStepDAL(this);
            Outbox = new MariaConsumerOutboxDAL(this);
        }
    }
}
