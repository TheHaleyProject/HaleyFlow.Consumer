using static Haley.Internal.QueryFields;

namespace Haley.Internal {
    internal static class QRY_ENTITY {
        /// <summary>
        /// Inserts a new entity row using the caller-supplied UUID.
        /// INSERT IGNORE is safe if the GUID was somehow already present (idempotent).
        /// </summary>
        public const string INSERT =
            $@"INSERT IGNORE INTO entity (id) VALUES ({ID});";

        public const string SELECT_BY_ID =
            $@"SELECT * FROM entity WHERE id = {ID};";
    }
}
