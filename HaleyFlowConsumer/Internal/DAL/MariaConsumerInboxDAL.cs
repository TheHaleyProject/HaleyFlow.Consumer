using Haley.Abstractions;
using Haley.Enums;
using Haley.Models;
using Haley.Utils;
using static Haley.Internal.QueryFields;

namespace Haley.Internal {
    internal sealed class MariaConsumerInboxDAL : MariaDALBase, IConsumerInboxDAL {
        public MariaConsumerInboxDAL(IDALUtilBase db) : base(db) { }

        public Task UpsertAsync(long wfId, string? paramsJson, DbExecutionLoad load = default)
            => Db.ExecAsync(QRY_INBOX.UPSERT, load,
                (WF_ID, wfId),
                (PARAMS_JSON, (object?)paramsJson ?? DBNull.Value));

        public Task SetStatusAsync(long wfId, InboxStatus status, string? error = null, DbExecutionLoad load = default)
            => Db.ExecAsync(QRY_INBOX.SET_STATUS, load,
                (WF_ID, wfId),
                (STATUS, (byte)status),
                (LAST_ERROR, (object?)error ?? DBNull.Value));

        public Task IncrementAttemptAsync(long wfId, DbExecutionLoad load = default)
            => Db.ExecAsync(QRY_INBOX.INCREMENT_ATTEMPT, load, (WF_ID, wfId));

        public Task<DbRow?> GetByWfIdAsync(long wfId, DbExecutionLoad load = default)
            => Db.RowAsync(QRY_INBOX.SELECT_BY_WF_ID, load, (WF_ID, wfId));
    }
}
