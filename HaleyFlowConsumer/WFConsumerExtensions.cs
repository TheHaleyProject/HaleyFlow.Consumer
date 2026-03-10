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

        public static IServiceCollection AddWorkFlowConsumerInitiator(this IServiceCollection services, IConfiguration configuration, string sectionName = "WorkFlowConsumer", bool autoStart = true, bool addDeferredInProcessProxy = true) {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);
            if (string.IsNullOrWhiteSpace(sectionName)) throw new ArgumentException("Section name is required.", nameof(sectionName));

            services.Configure<ConsumerInitiatorOptions>(configuration.GetSection(sectionName));
            return AddWorkFlowConsumerInitiatorCore(services, autoStart, addDeferredInProcessProxy);
        }

        public static IServiceCollection AddWorkFlowConsumerInitiator(this IServiceCollection services, Action<ConsumerInitiatorOptions> configureOptions, bool autoStart = true, bool addDeferredInProcessProxy = true) {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configureOptions);

            services.Configure(configureOptions);
            return AddWorkFlowConsumerInitiatorCore(services, autoStart, addDeferredInProcessProxy);
        }

        private static IServiceCollection AddWorkFlowConsumerInitiatorCore(IServiceCollection services, bool autoStart, bool addDeferredInProcessProxy) {
            services.TryAddSingleton<IAdapterGateway, AdapterGateway>();
            services.TryAddSingleton(sp => sp.GetRequiredService<IOptions<ConsumerInitiatorOptions>>().Value);
            services.TryAddSingleton<WorkFlowConsumerInitiatorService>();
            services.TryAddSingleton<IWorkFlowConsumerInitiatorService>(sp => sp.GetRequiredService<WorkFlowConsumerInitiatorService>());

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
