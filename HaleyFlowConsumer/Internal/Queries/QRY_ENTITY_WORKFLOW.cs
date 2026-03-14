using static Haley.Internal.QueryFields;

namespace Haley.Internal {
    internal static class QRY_ENTITY_WORKFLOW {
        /// <summary>
        /// Inserts a new entity-workflow mapping or updates instance_id/is_triggered on conflict
        /// (same def_name + entity + instance_id triple can't exist twice).
        /// </summary>
        public const string UPSERT =
            $@"INSERT INTO workflow (entity, def_name, instance_id, is_triggered)
               VALUES ({ENTITY}, {DEF_NAME}, {INSTANCE_ID}, {IS_TRIGGERED})
               ON DUPLICATE KEY UPDATE
                   instance_id  = VALUES(instance_id),
                   is_triggered = VALUES(is_triggered);
               SELECT LAST_INSERT_ID() AS id;";

        public const string SELECT_BY_ID =
            $@"SELECT * FROM workflow WHERE id = {ID};";

        /// <summary>Returns all workflows (all definitions) an entity is part of.</summary>
        public const string SELECT_BY_ENTITY =
            $@"SELECT * FROM workflow WHERE entity = {ENTITY} ORDER BY id DESC;";

        /// <summary>Sets instance_id and marks is_triggered = 1 after a successful engine trigger.</summary>
        public const string SET_TRIGGERED =
            $@"UPDATE workflow
               SET instance_id = {INSTANCE_ID}, is_triggered = 1
               WHERE entity = {ENTITY} AND def_name = {DEF_NAME};";
    }
}
