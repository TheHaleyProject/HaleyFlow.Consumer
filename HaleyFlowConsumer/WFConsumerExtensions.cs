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
        public static async Task<IWorkFlowConsumerProcessor> Build(this WorkFlowConsumerMaker input, IAdapterGateway agw) {
            //replace the sql contents, as only we know that.
            var adapterKey = await input.Initialize(agw); //Base names are already coming from the concrete implementation of DBInstanceMaker
            var dal = new MariaConsumerServiceDAL(agw, adapterKey);
            return new WorkFlowConsumerProcessor(input.EngineProxy,dal,input.ServiceProvider, input.Options);
        }

        public static async Task<IWorkFlowConsumerService> BuildService(this WorkFlowConsumerMaker input, IAdapterGateway agw) {
            var adapterKey = await input.Initialize(agw);
            var dal = new MariaConsumerServiceDAL(agw, adapterKey);
            return new WorkFlowConsumerService(dal);
        }

        public static IServiceCollection AddWorkFlowConsumerBootstrap(this IServiceCollection services, IConfiguration configuration, string sectionName = "WorkFlowConsumer", bool autoStart = true, bool addDeferredInProcessProxy = true) {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);
            if (string.IsNullOrWhiteSpace(sectionName)) throw new ArgumentException("Section name is required.", nameof(sectionName));

            services.Configure<ConsumerBootstrapOptions>(configuration.GetSection(sectionName));
            return AddWorkFlowConsumerBootstrapCore(services, autoStart, addDeferredInProcessProxy);
        }

        public static IServiceCollection AddWorkFlowConsumerBootstrap(this IServiceCollection services, Action<ConsumerBootstrapOptions> configureOptions, bool autoStart = true, bool addDeferredInProcessProxy = true) {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configureOptions);

            services.Configure(configureOptions);
            return AddWorkFlowConsumerBootstrapCore(services, autoStart, addDeferredInProcessProxy);
        }

        private static IServiceCollection AddWorkFlowConsumerBootstrapCore(IServiceCollection services, bool autoStart, bool addDeferredInProcessProxy) {
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

            services.TryAddSingleton(sp => sp.GetRequiredService<IOptions<ConsumerBootstrapOptions>>().Value);
            services.TryAddSingleton<WorkFlowConsumerBootstrap>();
            services.TryAddSingleton<IWorkFlowConsumerBootstrap>(sp => sp.GetRequiredService<WorkFlowConsumerBootstrap>());

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
                services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, WorkFlowConsumerHostedService>());
            }

            return services;
        }
    }
}
