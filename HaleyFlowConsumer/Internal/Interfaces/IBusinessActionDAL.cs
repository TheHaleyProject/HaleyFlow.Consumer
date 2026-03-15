using Haley.Enums;
using Haley.Models;
using System.Threading.Tasks;

namespace Haley.Internal {
    internal interface IBusinessActionDAL {
        /// <summary>
        /// Returns the existing business_action id when (instance_id, action_code) is already present.
        /// Inserts a new row only when not found.
        /// </summary>
        Task<long> UpsertReturnIdAsync(long instanceId, int actionCode, BusinessActionStatus status, DbExecutionLoad load = default);
        Task<BusinessActionRecord?> GetByIdAsync(long id, DbExecutionLoad load = default);
        Task<BusinessActionRecord?> GetByKeyAsync(long instanceId, int actionCode, DbExecutionLoad load = default);
        Task SetRunningAsync(long id, DbExecutionLoad load = default);
        Task SetCompletedAsync(long id, string? resultJson = null, DbExecutionLoad load = default);
        Task SetFailedAsync(long id, string? errorMessage = null, DbExecutionLoad load = default);
    }
}
