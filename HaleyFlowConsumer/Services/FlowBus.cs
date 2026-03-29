using Haley.Abstractions;
using Haley.Enums;
using Haley.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Haley.Services {
    /// <summary>
    /// Entry-point router for workflow initiation.
    ///
    /// Resolves the active executor based on GlobalMode and what is registered in DI:
    ///   GlobalMode == null  → Engine ?? Relay ?? throw
    ///   GlobalMode == Engine → Engine or throw
    ///   GlobalMode == Relay  → Relay or throw
    ///
    /// Per-call request.Mode overrides GlobalMode for that specific call.
    ///
    /// Business logic calls InitiateAsync — it has no knowledge of engine vs relay.
    /// </summary>
    public sealed class FlowBus : IFlowBus {
        private readonly IWorkFlowConsumerService? _consumerService;
        private readonly IWorkflowRelayService?    _relay;

        public FlowBusMode? GlobalMode { get; }

        public FlowBus(FlowBusOptions options, IWorkFlowConsumerService? consumerService = null, IWorkflowRelayService? relay = null) {
            GlobalMode = options?.Mode; //if the mode is not available, we have already defined the fall back scenarios in the intiate async.. so dont worry here.
            _consumerService = consumerService;
            _relay           = relay;
        }

        public async Task<IFeedback> InitiateAsync(FlowInitiateRequest request, CancellationToken ct = default) {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.WorkflowName)) return Feedback.Fail($"{HaleyFlowErrorCodes.BackfillMissingWorkflowName}: WorkflowName is required.");
            if (string.IsNullOrWhiteSpace(request.EntityId))     return Feedback.Fail($"{HaleyFlowErrorCodes.BackfillMissingEntityRef}: EntityId is required.");

            var mode = request.Mode ?? GlobalMode;

            if (mode == FlowBusMode.Engine)  return await InitiateEngineAsync(request, ct);
            if (mode == FlowBusMode.Relay)   return await InitiateRelayAsync(request, ct);

            // mode == null → auto: Engine ?? Relay ?? throw
            if (_consumerService != null) return await InitiateEngineAsync(request, ct);
            if (_relay  != null) return await InitiateRelayAsync(request, ct);

            return Feedback.Fail($"{HaleyFlowErrorCodes.FlowBusNoExecutor}: No workflow executor is registered. Register IWorkFlowConsumerService (engine) or IWorkflowRelayService (relay).");
        }

        private async Task<IFeedback> InitiateEngineAsync(FlowInitiateRequest request, CancellationToken ct) {
            if (_consumerService == null) return Feedback.Fail($"{HaleyFlowErrorCodes.FlowBusEngineNotRegistered}: Engine mode requested but IWorkFlowConsumerService is not registered.");
            if (string.IsNullOrWhiteSpace(request.StartEvent)) return Feedback.Fail($"{HaleyFlowErrorCodes.FlowBusMissingStartEvent}: StartEvent is required for engine mode.");

            var createRequest = new CreateWorkflowRequest {
                Event   = request.StartEvent!,
                Actor   = request.Actor,
                Payload = request.Payload as System.Collections.Generic.IReadOnlyDictionary<string, object>
            };

            var result = await _consumerService.CreateWorkflowAsync(request.EntityId, request.WorkflowName, createRequest, ct);
            return result.Applied
                ? Feedback.Ok(new { result.InstanceGuid })
                : Feedback.Fail(result.Reason ?? $"{HaleyFlowErrorCodes.FlowBusEngineRejected}: Engine rejected the initiation.", new { result.InstanceGuid });
        }

        private Task<IFeedback> InitiateRelayAsync(FlowInitiateRequest request, CancellationToken ct) {
            if (_relay == null) return Task.FromResult<IFeedback>(Feedback.Fail($"{HaleyFlowErrorCodes.FlowBusRelayNotRegistered}: Relay mode requested but IWorkflowRelayService is not registered."));
            return _relay.InitiateAsync(request, ct);
        }
    }
}
