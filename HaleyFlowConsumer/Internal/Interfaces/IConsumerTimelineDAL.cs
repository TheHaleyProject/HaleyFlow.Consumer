using Haley.Models;

namespace Haley.Internal {
    public interface IConsumerTimelineDAL {
        Task<ConsumerTimeline> GetByInstanceGuidAsync(string instanceGuid, DbExecutionLoad load = default);
    }
}
