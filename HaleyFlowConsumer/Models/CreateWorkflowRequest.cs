namespace Haley.Models {
    /// <summary>
    /// Request passed by the client when asking the consumer to create and trigger a workflow
    /// for a previously created entity.
    /// </summary>
    public sealed class CreateWorkflowRequest {
        /// <summary>Trigger event name or code string (e.g. "submit"). Required.</summary>
        public string Event { get; set; } = string.Empty;
        /// <summary>Actor identifier recorded on the transition (e.g. system user ID).</summary>
        public string? Actor { get; set; }
        /// <summary>Immutable metadata string stored on the workflow instance.</summary>
        public string? Metadata { get; set; }
        /// <summary>Arbitrary key/value payload recorded on the lifecycle entry.</summary>
        public IReadOnlyDictionary<string, object>? Payload { get; set; }
        /// <summary>When true, bypasses the ACK gate on the initial trigger.</summary>
        public bool SkipAckGate { get; set; }
    }
}
