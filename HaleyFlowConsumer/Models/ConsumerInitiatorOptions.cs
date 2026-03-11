using Haley.Enums;
using Microsoft.Extensions.Configuration;

namespace Haley.Models {
    public sealed class ConsumerInitiatorOptions {
        [ConfigurationKeyName("consumer_guid")]
        public string ConsumerGuid { get; set; } = string.Empty;

        [ConfigurationKeyName("env_code")]
        public int EnvCode { get; set; }

        [ConfigurationKeyName("env_name")]
        public string EnvDisplayName { get; set; } = "dev";

        public int MaxConcurrency { get; set; } = 5;
        public int BatchSize { get; set; } = 20;
        public int AckStatus { get; set; } = 1;
        public int TtlSeconds { get; set; } = 120;
        public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(30);
        public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);
        public TimeSpan OutboxInterval { get; set; } = TimeSpan.FromSeconds(15);
        public TimeSpan OutboxRetryDelay { get; set; } = TimeSpan.FromMinutes(2);
        public HandlerUpgrade DefaultHandlerUpgrade { get; set; } = HandlerUpgrade.Pinned;

        [ConfigurationKeyName("adapter_key")]
        public string ConsumerAdapterKey { get; set; } = string.Empty;

        [ConfigurationKeyName("wrapper_assemblies")]
        public List<string> WrapperAssemblies { get; set; } = new();
    }
}
