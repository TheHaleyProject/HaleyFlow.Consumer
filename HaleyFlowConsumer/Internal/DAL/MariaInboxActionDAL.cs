using Haley.Abstractions;
using Haley.Enums;
using Haley.Models;
using static Haley.Internal.QueryFields;

namespace Haley.Internal {
    internal sealed class MariaInboxActionDAL : MariaDALBase, IInboxActionDAL {
        public MariaInboxActionDAL(IDALUtilBase db) : base(db) { }

        public Task UpsertAsync(long inboxId, long actionId, InboxActionStatus status, string? error = null, DbExecutionLoad load = default)
            => Db.ExecAsync(QRY_INBOX_ACTION.UPSERT, load,
                (INBOX_ID, inboxId),
                (ACTION_ID, actionId),
                (STATUS, (byte)status),
                (LAST_ERROR, (object?)error ?? DBNull.Value));

        public Task<DbRow?> GetAsync(long inboxId, long actionId, DbExecutionLoad load = default)
            => Db.RowAsync(QRY_INBOX_ACTION.SELECT_BY_INBOX_AND_ACTION, load,
                (INBOX_ID, inboxId),
                (ACTION_ID, actionId));

        public Task<DbRows> GetAllByInboxAsync(long inboxId, DbExecutionLoad load = default)
            => Db.RowsAsync(QRY_INBOX_ACTION.SELECT_ALL_BY_INBOX, load, (INBOX_ID, inboxId));
    }
}
