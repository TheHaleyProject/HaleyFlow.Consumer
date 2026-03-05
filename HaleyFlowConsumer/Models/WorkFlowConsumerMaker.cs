using Haley.Abstractions;
using Haley.Services;

namespace Haley.Models {
    public sealed class WorkFlowConsumerMaker {
        public string AdapterKey { get; set; } = string.Empty;
        public string ConnectionString { get; set; } = string.Empty;
        public IServiceProvider? ServiceProvider { get; set; } = null;
        public ILifeCycleEventFeed? EventFeed { get; set; } = null;
        public ConsumerServiceOptions? Options { get; set; }
        public WorkFlowConsumerMaker() { }   
    }
}
