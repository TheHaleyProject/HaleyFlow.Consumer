using Haley.Enums;
using Haley.Models;
using System.Threading.Tasks;

namespace Haley.Internal {
    public interface IConsumerBusinessActionDAL {
        Task<long> UpsertReturnIdAsync(long consumerId, long defId, string entityId, int actionCode, BusinessActionStatus status, DbExecutionLoad load = default);
        Task<BusinessActionRecord?> GetByIdAsync(long id, DbExecutionLoad load = default);
        Task<BusinessActionRecord?> GetByKeyAsync(long consumerId, long defId, string entityId, int actionCode, DbExecutionLoad load = default);
        Task SetRunningAsync(long id, DbExecutionLoad load = default);
        Task SetCompletedAsync(long id, string? resultJson = null, DbExecutionLoad load = default);
        Task SetFailedAsync(long id, string? resultJson = null, DbExecutionLoad load = default);
    }
}
