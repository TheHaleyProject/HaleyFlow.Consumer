using Haley.Enums;
using Haley.Services;
using Microsoft.Extensions.Configuration;

namespace Haley.Models {
    public sealed class ConsumerServiceOptions : WorkFlowConsumerOptions {

        [ConfigurationKeyName("env_name")]
        public string EnvDisplayName { get; set; } = "dev";

        [ConfigurationKeyName("adapter_key")]
        public string ConsumerAdapterKey { get; set; } = string.Empty;

        [ConfigurationKeyName("wrapper_assemblies")]
        public List<string> WrapperAssemblies { get; set; } = new();
    }
}
