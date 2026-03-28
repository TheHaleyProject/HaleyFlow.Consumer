using Haley.Abstractions;
using Haley.Enums;
using Haley.Internal;
using Haley.Models;
using Haley.Services;
using Haley.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.Text;
using System.Threading.Tasks;

namespace Haley.Utils {
    public static class WFConsumerExtensions {
        public static async Task<IWorkFlowConsumerManager> Build(this WorkFlowConsumerMaker input, IAdapterGateway agw) {
            //replace the sql contents, as only we know that.
            if (input.EngineProxy == null) throw new InvalidOperationException("EngineProxy is required to build WorkFlowConsumerManager.");
            if (input.ServiceProvider == null) throw new InvalidOperationException("ServiceProvider is required to build WorkFlowConsumerManager.");
            var adapterKey = await input.Initialize(agw); //Base names are already coming from the concrete implementation of DBInstanceMaker
            var dal = new MariaServiceDAL(agw, adapterKey);
            return new WorkFlowConsumerManager(input.EngineProxy,dal,input.ServiceProvider, input.Options);
        }

        public static async Task<IWorkFlowConsumerService> BuildService(this WorkFlowConsumerMaker input, IAdapterGateway agw) {
            ArgumentNullException.ThrowIfNull(input);
            ArgumentNullException.ThrowIfNull(agw);

            if (input.EngineProxy == null) {
                throw new InvalidOperationException("EngineProxy is required to build WorkFlowConsumerService.");
            }

            //In Engine we dont try to get the service out but in consumer we need the service out
            var options = input.Options as ConsumerServiceOptions ?? new ConsumerServiceOptions {
                ConsumerGuid = input.Options?.ConsumerGuid ?? string.Empty,
                EnvCode = input.Options?.EnvCode ?? 0,
                MaxConcurrency = input.Options?.MaxConcurrency ?? 5,
                BatchSize = input.Options?.BatchSize ?? 20,
                AckStatus = input.Options?.AckStatus ?? 1,
                TtlSeconds = input.Options?.TtlSeconds ?? 120,
                HeartbeatInterval = input.Options?.HeartbeatInterval ?? TimeSpan.FromSeconds(30),
                PollInterval = input.Options?.PollInterval ?? TimeSpan.FromSeconds(5),
                OutboxInterval = input.Options?.OutboxInterval ?? TimeSpan.FromSeconds(15),
                OutboxRetryDelay = input.Options?.OutboxRetryDelay ?? TimeSpan.FromMinutes(2),
                DefaultHandlerUpgrade = input.Options?.DefaultHandlerUpgrade ?? HandlerUpgrade.Pinned
            };

            if (string.IsNullOrWhiteSpace(options.ConsumerAdapterKey)) {
                options.ConsumerAdapterKey = input.AdapterKey;
            }

            var provider = input.ServiceProvider;
            if (provider == null) {
                throw new InvalidOperationException("ServiceProvider is required to build WorkFlowConsumerService.");
            }
            return new WorkFlowConsumerService(options, agw, input.EngineProxy, provider);
        }

        public static IServiceCollection AddWorkFlowConsumerService(this IServiceCollection services, IConfiguration configuration, string sectionName = "WorkFlowConsumer", bool autoStart = true) {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);
            if (string.IsNullOrWhiteSpace(sectionName)) throw new ArgumentException("Section name is required.", nameof(sectionName));

            services.Configure<ConsumerServiceOptions>(configuration.GetSection(sectionName));
            return AddWorkFlowConsumerServiceCore(services, autoStart);
        }

        public static IServiceCollection AddWorkFlowConsumerService(this IServiceCollection services, Action<ConsumerServiceOptions> configureOptions, bool autoStart = true) {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configureOptions);

            services.Configure(configureOptions);
            return AddWorkFlowConsumerServiceCore(services, autoStart);
        }

        private static IServiceCollection AddWorkFlowConsumerServiceCore(IServiceCollection services, bool autoStart) {
            var hasIAdapter = services.Any(s => s.ServiceType == typeof(IAdapterGateway));
            var hasAdapter = services.Any(s => s.ServiceType == typeof(AdapterGateway));

            if (!hasIAdapter) {
                if (hasAdapter) {
                    services.TryAddSingleton<IAdapterGateway>(sp => sp.GetRequiredService<AdapterGateway>());
                } else {
                    services.TryAddSingleton<AdapterGateway>();
                    services.TryAddSingleton<IAdapterGateway>(sp => sp.GetRequiredService<AdapterGateway>());
                }
            }

            services.TryAddSingleton(sp => sp.GetRequiredService<IOptions<ConsumerServiceOptions>>().Value);
            services.TryAddSingleton<WorkFlowConsumerService>();
            services.TryAddSingleton<IWorkFlowConsumerService>(sp => sp.GetRequiredService<WorkFlowConsumerService>());

            // ILifeCycleEngineProxy must be registered by the caller before this call.
            // In-process host: call services.AddInProcessEngineProxy() from the engine package.
            // Remote host: register an HttpEngineProxy (or similar) explicitly.

            if (autoStart) {
                services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, WorkFlowConsumerBootstrap>());
            }

            return services;
        }

        /// <summary>
        /// Registers FlowBus as IFlowBus (singleton).
        ///
        /// FlowBus routes InitiateAsync to the engine (IWorkFlowConsumerService) or relay
        /// (IWorkflowRelayService) based on FlowBusOptions.Mode.
        /// When Mode is null: Engine takes priority over Relay.
        ///
        /// IWorkFlowConsumerService and IWorkflowRelayService are resolved optionally —
        /// whichever is registered is used. If neither is registered, InitiateAsync returns failure.
        /// </summary>
        public static IServiceCollection AddFlowBus(this IServiceCollection services, Action<FlowBusOptions>? configure = null) {
            ArgumentNullException.ThrowIfNull(services);
            if (configure != null) services.Configure(configure);
            services.TryAddSingleton(sp => sp.GetRequiredService<IOptions<FlowBusOptions>>().Value);
            services.TryAddSingleton<IFlowBus>(sp => new FlowBus(
                sp.GetRequiredService<FlowBusOptions>(),
                sp.GetService<IWorkFlowConsumerService>(),
                sp.GetService<IWorkflowRelayService>()));
            return services;
        }

        /// <summary>
        /// Registers WorkflowRelayHost as IWorkflowRelayService and IHostedService.
        ///
        /// At startup, WorkflowRelayHost scans all loaded assemblies for concrete WorkflowRelayBase
        /// subclasses decorated with [LifeCycleDefinition] and activates them from DI.
        ///
        /// Example:
        ///   services.AddWorkflowRelayService();
        ///   services.AddFlowBus(); // routes InitiateAsync → relay
        /// </summary>
        public static IServiceCollection AddWorkflowRelayService(this IServiceCollection services) {
            ArgumentNullException.ThrowIfNull(services);
            services.TryAddSingleton<WorkflowRelayHost>();
            services.TryAddSingleton<IWorkflowRelayService>(sp => sp.GetRequiredService<WorkflowRelayHost>());
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService>(sp => sp.GetRequiredService<WorkflowRelayHost>()));
            return services;
        }

        public static IReadOnlyList<Dictionary<string, object?>> ToConsumerInstanceDictionaries(this DbRows rows)
            => rows.ToDictionaries();

        public static IReadOnlyList<Dictionary<string, object?>> ToInstanceDictionaries(this DbRows rows)
            => rows.ToConsumerInstanceDictionaries();

        // Compatibility aliases for older callers.
        public static IReadOnlyList<Dictionary<string, object?>> ToConsumerWorkflowDictionaries(this DbRows rows)
            => rows.ToConsumerInstanceDictionaries();

        public static IReadOnlyList<Dictionary<string, object?>> ToWorkflowDictionaries(this DbRows rows)
            => rows.ToConsumerInstanceDictionaries();

        public static IReadOnlyList<Dictionary<string, object?>> ToInboxItemDictionaries(this DbRows rows) {
            var items = rows.ToDictionaries();
            for (var i = 0; i < items.Count; i++) {
                var item = items[i];
                item.MapEnumField<WorkflowKind>("kind");
                item.MapEnumField<HandlerUpgrade>("handler_upgrade");
            }

            return items;
        }

        public static IReadOnlyList<Dictionary<string, object?>> ToInboxStatusDictionaries(this DbRows rows) {
            var items = rows.ToDictionaries();
            for (var i = 0; i < items.Count; i++) {
                var item = items[i];
                item.MapEnumField<WorkflowKind>("kind");
                item.MapEnumField<InboxStatus>("status");
            }

            return items;
        }

        public static IReadOnlyList<Dictionary<string, object?>> ToInboxDictionaries(this DbRows rows)
            => rows.ToInboxStatusDictionaries();

        public static IReadOnlyList<Dictionary<string, object?>> ToOutboxDictionaries(this DbRows rows) {
            var items = rows.ToDictionaries();
            for (var i = 0; i < items.Count; i++) {
                var item = items[i];
                item.MapEnumField<WorkflowKind>("kind");
                item.MapEnumField<OutboxStatus>("status");
                item.MapEnumField<AckOutcome>("current_outcome");
            }

            return items;
        }

    }
}
