using Haley.Enums;
using Haley.Models;
using System.Threading.Tasks;

namespace Haley.Internal {
    public interface IConsumerBusinessActionDAL {
        /// <summary>
        /// Returns the existing business_action id when the logical key
        /// (def_id, entity_id, action_code) is already present.
        /// Falls back to insert/upsert only when the row does not exist yet, so repeated
        /// calls do not burn auto-increment values on duplicate-key updates.
        /// </summary>
        Task<long> UpsertReturnIdAsync(long defId, string entityId, int actionCode, BusinessActionStatus status, DbExecutionLoad load = default);
        Task<BusinessActionRecord?> GetByIdAsync(long id, DbExecutionLoad load = default);
        Task<BusinessActionRecord?> GetByKeyAsync(long defId, string entityId, int actionCode, DbExecutionLoad load = default);
        Task SetRunningAsync(long id, DbExecutionLoad load = default);
        Task SetCompletedAsync(long id, string? resultJson = null, DbExecutionLoad load = default);
        Task SetFailedAsync(long id, string? resultJson = null, DbExecutionLoad load = default);
    }
}
