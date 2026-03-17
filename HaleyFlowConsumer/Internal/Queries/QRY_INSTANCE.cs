using static Haley.Internal.QueryFields;

namespace Haley.Internal {
    internal static class QRY_INSTANCE {
        /// <summary>
        /// Inserts a new instance row or returns the existing row id when the GUID is already present.
        /// INSERT IGNORE leaves the auto_increment counter untouched on duplicate.
        /// created is not overwritten on duplicate — preserves the first-seen timestamp.
        /// </summary>
        public const string INSERT_IGNORE =
            $@"INSERT IGNORE INTO instance (guid, def_name, def_version_value, entity_guid, created)
               VALUES ({GUID}, {DEF_NAME}, {DEF_VERSION}, {ENTITY_GUID}, {OCCURRED});";

        public const string SELECT_ID_BY_GUID =
            $@"SELECT id FROM instance WHERE guid = {GUID} LIMIT 1;";

        public const string SELECT_BY_ID =
            $@"SELECT inst.id, inst.guid, inst.def_name, inst.def_version_value AS def_version, inst.entity_guid, inst.created
               FROM instance inst
               WHERE inst.id = {ID};";

        public const string SELECT_BY_GUID =
            $@"SELECT inst.id, inst.guid, inst.def_name, inst.def_version_value AS def_version, inst.entity_guid, inst.created
               FROM instance inst
               WHERE inst.guid = {GUID}
               LIMIT 1;";

        public const string LIST_PAGED =
            $@"SELECT inst.id, inst.guid, inst.def_name, inst.def_version_value AS def_version, inst.entity_guid, inst.created
               FROM instance inst
               WHERE (NULLIF(TRIM({ENTITY_GUID}), '') IS NULL OR inst.entity_guid = lower(trim({ENTITY_GUID})))
                 AND (NULLIF(TRIM({DEF_NAME}), '') IS NULL OR inst.def_name = trim({DEF_NAME}))
                 AND (NULLIF(TRIM({INSTANCE_GUID}), '') IS NULL OR inst.guid = trim({INSTANCE_GUID}))
               ORDER BY inst.id DESC
               LIMIT {TAKE} OFFSET {SKIP};";

        public const string SELECT_BY_ENTITY =
            $@"SELECT inst.id, inst.guid, inst.def_name, inst.def_version_value AS def_version, inst.entity_guid, inst.created
               FROM instance inst
               WHERE inst.entity_guid = lower(trim({ENTITY_GUID}))
               ORDER BY inst.id DESC;";
    }
}
