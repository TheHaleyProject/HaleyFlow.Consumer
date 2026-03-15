using Haley.Abstractions;
using Haley.Models;
using Haley.Utils;
using static Haley.Internal.QueryFields;
using static Haley.Internal.KeyConstants;

namespace Haley.Internal {
    internal sealed class MariaInstanceDAL : MariaDALBase, IInstanceDAL {
        public MariaInstanceDAL(IDALUtilBase db) : base(db) { }

        public async Task<long> UpsertAsync(InstanceRecord record, DbExecutionLoad load = default) {
            var guid = record.Guid;
            // Fast path: row already exists (common case — same instance processes many events).
            var existing = await Db.ScalarAsync<long?>(QRY_INSTANCE.SELECT_ID_BY_GUID, load, (GUID, guid));
            if (existing.HasValue && existing.Value > 0) return existing.Value;

            // INSERT IGNORE never burns an auto_increment slot on duplicate.
            await Db.ExecAsync(QRY_INSTANCE.INSERT_IGNORE, load,
                (GUID, guid),
                (DEF_NAME, record.DefName),
                (DEF_VERSION, record.DefVersion),
                (ENTITY_GUID, record.EntityGuid.ToLowerInvariant()),
                (OCCURRED, record.Created));

            var id = await Db.ScalarAsync<long?>(QRY_INSTANCE.SELECT_ID_BY_GUID, load, (GUID, guid));
            if (id == null || id.Value <= 0)
                throw new InvalidOperationException($"Instance upsert failed for guid={guid}.");
            return id.Value;
        }

        public async Task<InstanceRecord?> GetByIdAsync(long id, DbExecutionLoad load = default) {
            var row = await Db.RowAsync(QRY_INSTANCE.SELECT_BY_ID, load, (ID, id));
            return row == null ? null : MapRow(row);
        }

        public async Task<InstanceRecord?> GetByGuidAsync(string instanceGuid, DbExecutionLoad load = default) {
            var row = await Db.RowAsync(QRY_INSTANCE.SELECT_BY_GUID, load, (GUID, instanceGuid?.Trim() ?? string.Empty));
            return row == null ? null : MapRow(row);
        }

        public Task<DbRows> ListPagedAsync(ConsumerInstanceFilter filter, DbExecutionLoad load = default) {
            filter ??= new ConsumerInstanceFilter();
            return Db.RowsAsync(QRY_INSTANCE.LIST_PAGED, load,
                (ENTITY_GUID, string.IsNullOrWhiteSpace(filter.EntityGuid) ? DBNull.Value : filter.EntityGuid.Trim()),
                (DEF_NAME, string.IsNullOrWhiteSpace(filter.DefName) ? DBNull.Value : filter.DefName.Trim()),
                (INSTANCE_GUID, string.IsNullOrWhiteSpace(filter.Guid) ? DBNull.Value : filter.Guid.Trim()),
                (TAKE, filter.Take),
                (SKIP, filter.Skip));
        }

        public Task<DbRows> GetByEntityAsync(string entityGuid, DbExecutionLoad load = default)
            => Db.RowsAsync(QRY_INSTANCE.SELECT_BY_ENTITY, load, (ENTITY_GUID, entityGuid));

        private static InstanceRecord MapRow(DbRow r) => new InstanceRecord {
            Id        = r.GetLong(KEY_ID),
            Guid      = r.GetString(KEY_GUID) ?? string.Empty,
            DefName   = r.GetString(KEY_DEF_NAME) ?? string.Empty,
            DefVersion = r.GetNullableInt(KEY_DEF_VERSION) ?? 0,
            EntityGuid = r.GetString(KEY_ENTITY_GUID) ?? string.Empty,
            Created   = r.GetDateTime(KEY_CREATED) ?? default,
        };
    }
}
