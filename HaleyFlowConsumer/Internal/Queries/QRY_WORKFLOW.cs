using static Haley.Internal.QueryFields;

namespace Haley.Internal {
    internal static class QRY_WORKFLOW {

        public const string UPSERT =
            $@"INSERT INTO workflow (ack_guid, entity_id, kind, consumer_id, def_id, def_version_id, instance_guid, on_success, on_failure, occurred, event_code, route)
               VALUES ({ACK_GUID}, lower(trim({ENTITY_ID})), {KIND}, {CONSUMER_ID}, {DEF_ID}, {DEF_VERSION_ID}, {INSTANCE_GUID}, {ON_SUCCESS}, {ON_FAILURE}, {OCCURRED}, {EVENT_CODE}, {ROUTE})
               ON DUPLICATE KEY UPDATE id = LAST_INSERT_ID(id);";

        public const string SELECT_BY_ID =
            $@"SELECT * FROM workflow WHERE id = {WF_ID};";

        public const string GET_PINNED_HANDLER_VERSION =
            $@"SELECT handler_version FROM workflow
               WHERE def_id = {DEF_ID} AND entity_id = lower(trim({ENTITY_ID})) AND handler_version IS NOT NULL
               ORDER BY id ASC LIMIT 1;";

        public const string SET_HANDLER_VERSION =
            $@"UPDATE workflow SET handler_version = {HANDLER_VERSION}, handler_upgrade = {HANDLER_UPGRADE} WHERE id = {WF_ID};";
    }
}
