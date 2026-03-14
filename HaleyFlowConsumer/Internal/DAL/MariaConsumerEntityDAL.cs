using Haley.Abstractions;
using Haley.Utils;
using static Haley.Internal.QueryFields;
using static Haley.Internal.KeyConstants;
using Haley.Models;

namespace Haley.Internal {
    internal sealed class MariaConsumerEntityDAL : MariaDALBase, IConsumerEntityDAL {
        public MariaConsumerEntityDAL(IDALUtilBase db) : base(db) { }

        public async Task<string> CreateAsync(DbExecutionLoad load = default) {
            var guid = Guid.NewGuid().ToString("D");  // standard hyphenated UUID, fits varchar(42)
            await Db.ExecAsync(QRY_ENTITY.INSERT, load, (ID, guid));
            return guid;
        }
    }
}
