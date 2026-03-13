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
            var dal = new MariaConsumerServiceDAL(agw, adapterKey);
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

        public static IServiceCollection AddWorkFlowConsumerService(this IServiceCollection services, IConfiguration configuration, string sectionName = "WorkFlowConsumer", bool autoStart = true, bool addDeferredInProcessProxy = true) {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);
            if (string.IsNullOrWhiteSpace(sectionName)) throw new ArgumentException("Section name is required.", nameof(sectionName));

            services.Configure<ConsumerServiceOptions>(configuration.GetSection(sectionName));
            return AddWorkFlowConsumerServiceCore(services, autoStart, addDeferredInProcessProxy);
        }

        public static IServiceCollection AddWorkFlowConsumerService(this IServiceCollection services, Action<ConsumerServiceOptions> configureOptions, bool autoStart = true, bool addDeferredInProcessProxy = true) {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configureOptions);

            services.Configure(configureOptions);
            return AddWorkFlowConsumerServiceCore(services, autoStart, addDeferredInProcessProxy);
        }

        private static IServiceCollection AddWorkFlowConsumerServiceCore(IServiceCollection services, bool autoStart, bool addDeferredInProcessProxy) {
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

            if (addDeferredInProcessProxy) {
                services.TryAddSingleton<ILifeCycleEngineProxy>(sp => {
                    var accessor = sp.GetService<IWorkFlowEngineAccessor>();
                    if (accessor == null) {
                        throw new InvalidOperationException("No ILifeCycleEngineProxy was registered, and IWorkFlowEngineAccessor was not found for in-process fallback. Register ILifeCycleEngineProxy explicitly or register engine service first.");
                    }
                    return new DeferredInProcessEngineProxy(accessor);
                });
            }

            if (autoStart) {
                services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, WorkFlowConsumerBootstrap>());
            }

            return services;
        }

        public static IReadOnlyList<Dictionary<string, object?>> ToWorkflowDictionaries(this DbRows rows) {
            var items = rows.ToDictionaries();
            for (var i = 0; i < items.Count; i++) {
                var item = items[i];
                item.MapEnumField<WorkflowKind>("kind");
                item.MapEnumField<InboxStatus>("inbox_status");
                item.MapEnumField<OutboxStatus>("outbox_status");
                item.MapEnumField<AckOutcome>("current_outcome");
            }

            return items;
        }

        public static IReadOnlyList<Dictionary<string, object?>> ToInboxDictionaries(this DbRows rows) {
            var items = rows.ToDictionaries();
            for (var i = 0; i < items.Count; i++) {
                var item = items[i];
                item.MapEnumField<WorkflowKind>("kind");
                item.MapEnumField<InboxStatus>("status");
            }

            return items;
        }

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
