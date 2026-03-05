using Haley.Abstractions;
using Haley.Enums;
using Haley.Models;
using static Haley.Internal.QueryFields;

namespace Haley.Internal {
    internal sealed class MariaConsumerInboxStepDAL : MariaDALBase, IConsumerInboxStepDAL {
        public MariaConsumerInboxStepDAL(IDALUtilBase db) : base(db) { }

        public Task UpsertStepAsync(long inboxId, int stepCode, InboxStepStatus status, string? result = null, string? error = null, DbExecutionLoad load = default)
            => Db.ExecAsync(QRY_INBOX_STEP.UPSERT, load,
                (INBOX_ID, inboxId),
                (STEP_CODE, stepCode),
                (STATUS, (byte)status),
                (RESULT_JSON, (object?)result ?? DBNull.Value),
                (LAST_ERROR, (object?)error ?? DBNull.Value));

        public Task<DbRow?> GetStepAsync(long inboxId, int stepCode, DbExecutionLoad load = default)
            => Db.RowAsync(QRY_INBOX_STEP.SELECT_BY_INBOX_AND_CODE, load,
                (INBOX_ID, inboxId),
                (STEP_CODE, stepCode));

        public Task<DbRows> GetStepsAsync(long inboxId, DbExecutionLoad load = default)
            => Db.RowsAsync(QRY_INBOX_STEP.SELECT_ALL_BY_INBOX, load, (INBOX_ID, inboxId));
    }
}
