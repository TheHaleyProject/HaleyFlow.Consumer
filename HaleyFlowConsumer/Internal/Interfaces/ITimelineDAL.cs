using Haley.Models;

namespace Haley.Internal {
    internal interface ITimelineDAL {
        Task<ConsumerTimeline> GetByInstanceGuidAsync(string instanceGuid, DbExecutionLoad load = default);
    }
}
