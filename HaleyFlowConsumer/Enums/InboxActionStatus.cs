namespace Haley.Enums {
    /// <summary>Per-delivery status of a business action attempt recorded in inbox_action.</summary>
    public enum InboxActionStatus : byte {
        Attempted = 1,
        Completed = 2,
        Failed = 3
    }
}
