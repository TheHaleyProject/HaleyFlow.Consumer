using static Haley.Internal.QueryFields;

namespace Haley.Internal {
    internal static class QRY_WORKFLOW {
        public const string SELECT_ID_BY_ACK_GUID_AND_CONSUMER =
            $@"SELECT id FROM workflow
               WHERE consumer_id = {CONSUMER_ID} AND ack_guid = {ACK_GUID}
               LIMIT 1;";

        public const string UPSERT =
            $@"INSERT INTO workflow (ack_guid, entity_id, kind, consumer_id, def_id, def_version_id, instance_guid, on_success, on_failure, occurred, event_code, route, run_count)
               VALUES ({ACK_GUID}, lower(trim({ENTITY_ID})), {KIND}, {CONSUMER_ID}, {DEF_ID}, {DEF_VERSION_ID}, {INSTANCE_GUID}, {ON_SUCCESS}, {ON_FAILURE}, {OCCURRED}, {EVENT_CODE}, {ROUTE}, {RUN_COUNT})
               ON DUPLICATE KEY UPDATE id = LAST_INSERT_ID(id); SELECT LAST_INSERT_ID() AS id;";

        public const string UPDATE =
            $@"UPDATE workflow
               SET entity_id = lower(trim({ENTITY_ID})),
                   kind = {KIND},
                   def_id = {DEF_ID},
                   def_version_id = {DEF_VERSION_ID},
                   instance_guid = {INSTANCE_GUID},
                   on_success = {ON_SUCCESS},
                   on_failure = {ON_FAILURE},
                   occurred = {OCCURRED},
                   event_code = {EVENT_CODE},
                   route = {ROUTE},
                   run_count = {RUN_COUNT}
               WHERE id = {WF_ID};";

        public const string SELECT_BY_ID =
            $@"SELECT * FROM workflow WHERE id = {WF_ID};";

        public const string GET_PINNED_HANDLER_VERSION =
            $@"SELECT handler_version FROM workflow
               WHERE def_id = {DEF_ID} AND entity_id = lower(trim({ENTITY_ID})) AND handler_version IS NOT NULL
               ORDER BY id ASC LIMIT 1;";

        public const string SET_HANDLER_VERSION =
            $@"UPDATE workflow SET handler_version = {HANDLER_VERSION}, handler_upgrade = {HANDLER_UPGRADE} WHERE id = {WF_ID};";

        public const string LIST_PAGED =
            $@"SELECT w.id, w.ack_guid, w.entity_id, w.kind, w.consumer_id, w.def_id, w.def_version_id,
              w.instance_guid, w.event_code, w.route, w.occurred, w.created,
              i.status AS inbox_status, i.attempt_count,
              o.status AS outbox_status, o.current_outcome, o.next_retry_at
       FROM workflow w
       LEFT JOIN inbox i ON i.wf_id = w.id
       LEFT JOIN outbox o ON o.wf_id = w.id
       ORDER BY w.id DESC
       LIMIT {TAKE} OFFSET {SKIP};";
    }
}
