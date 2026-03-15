using static Haley.Internal.QueryFields;

namespace Haley.Internal {
    internal static class QRY_WORKFLOW {
        /// <summary>
        /// Inserts a new entity-workflow mapping or updates the existing row matched by
        /// UNIQUE(def_name, entity). Instance id is preserved when the incoming value is empty.
        /// </summary>
        public const string UPSERT =
            $@"INSERT INTO workflow (entity, def_name, instance_id, is_triggered)
               VALUES ({ENTITY}, {DEF_NAME}, NULLIF({INSTANCE_ID}, ''), {IS_TRIGGERED})
               ON DUPLICATE KEY UPDATE
                   instance_id  = CASE
                                    WHEN NULLIF(VALUES(instance_id), '') IS NULL THEN instance_id
                                    ELSE VALUES(instance_id)
                                  END,
                   is_triggered = CASE
                                    WHEN VALUES(is_triggered) = 1 THEN 1
                                    ELSE is_triggered
                                  END,
                   id           = LAST_INSERT_ID(id);
                SELECT LAST_INSERT_ID() AS id;";

        public const string SELECT_BY_ID =
            $@"SELECT * FROM workflow WHERE id = {ID};";

        public const string LIST_PAGED =
            $@"SELECT w.id, w.entity, w.def_name, w.instance_id,
                      CASE WHEN w.is_triggered = b'1' THEN 1 ELSE 0 END AS is_triggered
               FROM workflow w
               WHERE (NULLIF(TRIM({ENTITY}), '') IS NULL OR w.entity = trim({ENTITY}))
                 AND (NULLIF(TRIM({DEF_NAME}), '') IS NULL OR w.def_name = trim({DEF_NAME}))
                 AND (NULLIF(TRIM({INSTANCE_ID}), '') IS NULL OR w.instance_id = trim({INSTANCE_ID}))
                 AND ({IS_TRIGGERED} IS NULL OR CASE WHEN w.is_triggered = b'1' THEN 1 ELSE 0 END = {IS_TRIGGERED})
               ORDER BY w.id DESC
               LIMIT {TAKE} OFFSET {SKIP};";

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
