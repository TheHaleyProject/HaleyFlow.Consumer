using Haley.Enums;
using System.Collections.Generic;
using System.Threading.Tasks;
using Haley.Models;

namespace Haley.Internal {
    public interface IConsumerInboxStepDAL {
        /// <summary>Upserts a step row. On duplicate (inbox_id, step_code) updates status fields.</summary>
        Task UpsertStepAsync(long inboxId, int stepCode, InboxStepStatus status, string? result = null, string? error = null, DbExecutionLoad load = default);
        Task<DbRow?> GetStepAsync(long inboxId, int stepCode, DbExecutionLoad load = default);
        Task<DbRows> GetStepsAsync(long inboxId, DbExecutionLoad load = default);
    }
}
