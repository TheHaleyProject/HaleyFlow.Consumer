using Haley.Enums;
using System.Threading.Tasks;
using Haley.Models;

namespace Haley.Internal {
    public interface IConsumerInboxDAL {
        /// <summary>INSERT IGNORE — creates inbox row on first receive; no-op if already exists.</summary>
        Task UpsertAsync(long wfId, string? paramsJson, DbExecutionLoad load = default);
        Task SetStatusAsync(long wfId, InboxStatus status, string? error = null, DbExecutionLoad load = default);
        Task IncrementAttemptAsync(long wfId, DbExecutionLoad load = default);
        Task<DbRow?> GetByWfIdAsync(long wfId, DbExecutionLoad load = default);
    }
}
