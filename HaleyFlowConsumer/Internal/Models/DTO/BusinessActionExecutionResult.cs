namespace Haley.Models {
    public sealed class BusinessActionExecutionResult {
        public long ActionId { get; set; }
        public bool Executed { get; set; }
        public bool AlreadyCompleted { get; set; }
        public string? ResultJson { get; set; }
    }
}
