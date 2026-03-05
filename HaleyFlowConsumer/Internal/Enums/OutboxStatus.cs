namespace Haley.Enums {
    public enum OutboxStatus : byte {
        Pending = 1,
        Sent = 2,
        Confirmed = 3,
        Failed = 4
    }
}
