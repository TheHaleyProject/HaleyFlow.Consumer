using Haley.Abstractions;
using Haley.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Haley.Services {
    /// <summary>
    /// Hosted service that owns all WorkflowRelayBase instances for in-process relay execution.
    /// Implements IWorkflowRelayService so FlowBus can route InitiateAsync to it.
    ///
    /// At startup: scans all loaded assemblies for concrete WorkflowRelayBase subclasses
    /// decorated with [LifeCycleDefinition], activates them from DI, calls Initialize() on each.
    /// On InitiateAsync: routes by WorkflowName to the correct relay and calls NextAsync.
    /// </summary>
    public sealed class WorkflowRelayHost : IHostedService, IWorkflowRelayService {
        private readonly IServiceProvider _sp;
        private readonly Dictionary<string, WorkflowRelayBase> _index = new(StringComparer.OrdinalIgnoreCase);

        public WorkflowRelayHost(IServiceProvider sp) {
            _sp = sp ?? throw new ArgumentNullException(nameof(sp));
        }

        public Task StartAsync(CancellationToken ct) {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()) {
                foreach (var type in asm.GetTypes()) {
                    if (type.IsAbstract || !typeof(WorkflowRelayBase).IsAssignableFrom(type)) continue;
                    var attr = type.GetCustomAttribute<LifeCycleDefinitionAttribute>();
                    if (attr == null) continue;
                    var instance = (WorkflowRelayBase)ActivatorUtilities.CreateInstance(_sp, type);
                    instance.Initialize();
                    _index[attr.Name] = instance;
                }
            }
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

        public async Task<IFeedback> InitiateAsync(FlowInitiateRequest request, CancellationToken ct = default) {
            if (request == null) throw new ArgumentNullException(nameof(request));

            if (!_index.TryGetValue(request.WorkflowName, out var relayBase))
                return Feedback.Fail($"No relay registered for workflow '{request.WorkflowName}'.");

            if (!int.TryParse(request.StartEvent, out var startEventCode))
                return Feedback.Fail($"StartEvent '{request.StartEvent}' is not a valid event code for relay mode.");

            var ctx = new RelayContext {
                EntityRef = request.EntityId,
                Actor     = request.Actor,
                Payload   = request.Payload,
            };

            var result = await relayBase.Relay.NextAsync(ctx, startEventCode, ct);
            return result.Advanced
                ? Feedback.Ok(new { result.NewState })
                : Feedback.Fail(result.Reason ?? "Relay blocked.", new { result.Reason });
        }
    }
}
