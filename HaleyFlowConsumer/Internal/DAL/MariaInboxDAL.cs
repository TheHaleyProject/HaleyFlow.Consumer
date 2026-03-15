using Haley.Abstractions;
using Haley.Enums;
using Haley.Models;
using Haley.Utils;
using static Haley.Internal.QueryFields;
using static Haley.Internal.KeyConstants;

namespace Haley.Internal {
    internal sealed class MariaInboxDAL : MariaDALBase, IInboxDAL {
        public MariaInboxDAL(IDALUtilBase db) : base(db) { }

        public async Task<(long inboxId, bool isNew)> UpsertAsync(InboxRecord r, DbExecutionLoad load = default) {
            // Fast path: ack_guid seen before (engine retry).
            var existingId = await Db.ScalarAsync<long?>(QRY_INBOX.SELECT_ID_BY_ACK_GUID, load, (ACK_GUID, r.AckGuid));
            if (existingId.HasValue && existingId.Value > 0)
                return (existingId.Value, false);

            // INSERT IGNORE never burns an auto_increment slot on duplicate.
            await Db.ExecAsync(QRY_INBOX.INSERT_IGNORE, load,
                (ACK_GUID, r.AckGuid),
                (KIND, (byte)r.Kind),
                (INSTANCE_ID, r.InstanceId),
                (ON_SUCCESS, (object?)r.OnSuccess ?? DBNull.Value),
                (ON_FAILURE, (object?)r.OnFailure ?? DBNull.Value),
                (OCCURRED, r.Occurred),
                (EVENT_CODE, (object?)r.EventCode ?? DBNull.Value),
                (ROUTE, (object?)r.Route ?? DBNull.Value),
                (RUN_COUNT, r.RunCount));

            var id = await Db.ScalarAsync<long?>(QRY_INBOX.SELECT_ID_BY_ACK_GUID, load, (ACK_GUID, r.AckGuid));
            if (id == null || id.Value <= 0) throw new InvalidOperationException("inbox upsert failed.");

            var row = await GetByIdAsync(id.Value, load);
            return (id.Value, row?.HandlerVersion == null);
        }

        public async Task<InboxRecord?> GetByIdAsync(long inboxId, DbExecutionLoad load = default) {
            var row = await Db.RowAsync(QRY_INBOX.SELECT_BY_ID, load, (INBOX_ROW_ID, inboxId));
            return row == null ? null : MapRow(row);
        }

        public async Task<int?> GetPinnedHandlerVersionAsync(long instanceId, DbExecutionLoad load = default)
            => await Db.ScalarAsync<int?>(QRY_INBOX.GET_PINNED_HANDLER_VERSION, load,
                (INSTANCE_ID, instanceId));

        public Task SetHandlerVersionAsync(long inboxId, int handlerVersion, HandlerUpgrade upgrade, DbExecutionLoad load = default)
            => Db.ExecAsync(QRY_INBOX.SET_HANDLER_VERSION, load,
                (INBOX_ROW_ID, inboxId),
                (HANDLER_VERSION, handlerVersion),
                (HANDLER_UPGRADE, (byte)upgrade));

        public Task<DbRows> ListPagedAsync(ConsumerInboxFilter filter, DbExecutionLoad load = default) {
            filter ??= new ConsumerInboxFilter();
            return Db.RowsAsync(QRY_INBOX.LIST_PAGED, load,
                (KIND, filter.Kind.HasValue ? (object?)(byte)filter.Kind.Value : DBNull.Value),
                (INSTANCE_GUID, string.IsNullOrWhiteSpace(filter.InstanceGuid) ? DBNull.Value : filter.InstanceGuid.Trim()),
                (ACK_GUID, string.IsNullOrWhiteSpace(filter.AckGuid) ? DBNull.Value : filter.AckGuid.Trim()),
                (ROUTE, string.IsNullOrWhiteSpace(filter.Route) ? DBNull.Value : filter.Route.Trim()),
                (EVENT_CODE, filter.EventCode.HasValue ? (object?)filter.EventCode.Value : DBNull.Value),
                (TAKE, filter.Take),
                (SKIP, filter.Skip));
        }

        private static InboxRecord MapRow(DbRow r) => new InboxRecord {
            Id             = r.GetLong(KEY_ID),
            AckGuid        = r.GetString(KEY_ACK_GUID) ?? string.Empty,
            Kind           = (WorkflowKind)r.GetByte(KEY_KIND),
            InstanceId     = r.GetLong(KEY_INSTANCE_ID),
            HandlerVersion = r.GetNullableInt(KEY_HANDLER_VERSION),
            OnSuccess      = r.GetNullableInt(KEY_ON_SUCCESS),
            OnFailure      = r.GetNullableInt(KEY_ON_FAILURE),
            Occurred       = r.GetDateTime(KEY_OCCURRED) ?? r.GetDateTime(KEY_CREATED) ?? default,
            EventCode      = r.GetNullableInt(KEY_EVENT_CODE),
            Route          = r.GetString(KEY_ROUTE),
            RunCount       = r.GetNullableInt(KEY_RUN_COUNT) ?? 1,
            Created        = r.GetDateTime(KEY_CREATED) ?? r.GetDateTime(KEY_OCCURRED) ?? default,
            HandlerUpgrade = (HandlerUpgrade)(r.GetNullableByte(KEY_HANDLER_UPGRADE) ?? (byte)HandlerUpgrade.Pinned)
        };
    }
}
