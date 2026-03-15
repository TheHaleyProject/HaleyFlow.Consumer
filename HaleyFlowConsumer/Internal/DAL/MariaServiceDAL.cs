using Haley.Abstractions;
using Haley.Utils;

namespace Haley.Internal {
    internal sealed class MariaServiceDAL : DALUtilBase, IServiceDAL {
        public IInboxDAL Inbox { get; }
        public IInboxStatusDAL InboxStatus { get; }
        public IInboxActionDAL InboxAction { get; }
        public IBusinessActionDAL BusinessAction { get; }
        public IOutboxDAL Outbox { get; }
        public IInstanceDAL Instance { get; }
        public ITimelineDAL Timeline { get; }

        public MariaServiceDAL(IAdapterGateway agw, string key) : base(agw, key) {
            Inbox          = new MariaInboxDAL(this);
            InboxStatus    = new MariaInboxStatusDAL(this);
            InboxAction    = new MariaInboxActionDAL(this);
            BusinessAction = new MariaBusinessActionDAL(this);
            Outbox         = new MariaOutboxDAL(this);
            Instance       = new MariaInstanceDAL(this);
            Timeline       = new MariaTimelineDAL(this);
        }
    }
}
