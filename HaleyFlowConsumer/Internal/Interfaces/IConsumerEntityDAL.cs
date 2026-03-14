namespace Haley.Internal {
    /// <summary>Manages the <c>entity</c> table — the UUID generator for business entities.</summary>
    public interface IConsumerEntityDAL {
        /// <summary>
        /// Generates a new UUID, inserts it into the entity table, and returns the GUID string.
        /// The caller stores this ID and passes it to the client; the client uses it as their
        /// cross-system entity reference.
        /// </summary>
        Task<string> CreateAsync(DbExecutionLoad load = default);
    }
}
