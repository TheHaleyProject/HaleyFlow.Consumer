using Haley.Abstractions;
using Haley.Enums;
using Haley.Internal;
using Haley.Models;
using Haley.Services;
using Haley.Utils;
using System;
using System.Text;
using System.Threading.Tasks;

namespace Haley.Utils {
    public static class WFConsumerExtensions {
        public static async Task<IWorkFlowConsumerService> Build(this WorkFlowConsumerMaker input, IAdapterGateway agw) {
            //replace the sql contents, as only we know that.
            var adapterKey = await input.Initialize(agw); //Base names are already coming from the concrete implementation of DBInstanceMaker
            var dal = new MariaConsumerServiceDAL(agw, adapterKey);
            return new WorkFlowConsumerService(input.EngineProxy,dal,input.ServiceProvider, input.Options);
        }

        public static async Task<IConsumerAdminService> BuildAdmin(this WorkFlowConsumerMaker input, IAdapterGateway agw) {
            var adapterKey = await input.Initialize(agw);
            var dal = new MariaConsumerServiceDAL(agw, adapterKey);
            return new ConsumerAdminService(dal);
        }
    }
}
