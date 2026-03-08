using Haley.Abstractions;
using Haley.Services;
using Haley.Utils;
using System.Text;

namespace Haley.Models {

    public sealed class WorkFlowConsumerMaker : DbInstanceMaker {
        const string FALLBACK_DB_NAME = "wf_consumer";
        const string EMBEDDED_SQL_RESOURCE = "Haley.Scripts.consumer.sql";
        const string REPLACE_DBNAME = "lc_consumer";
        public ILifeCycleEngineProxy? EngineProxy { get; set; } = null;
        public ConsumerServiceOptions? Options { get; set; }
        public WorkFlowConsumerMaker() {
            FallbackDbName = FALLBACK_DB_NAME;
            ReplaceDbName = REPLACE_DBNAME;
            SqlContent = Encoding.UTF8.GetString(ResourceUtils.GetEmbeddedResource(EMBEDDED_SQL_RESOURCE));
        }   
    }
}
