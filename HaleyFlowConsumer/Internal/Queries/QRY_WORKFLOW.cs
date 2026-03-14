using static Haley.Internal.QueryFields;

namespace Haley.Internal {
    internal static class QRY_WORKFLOW {
        // ack_guid is globally unique (UUID from engine) — no need for consumer_id in the check.
        public const string SELECT_ID_BY_ACK_GUID =
            $@"SELECT id FROM inbox
               WHERE ack_guid = {ACK_GUID}
               LIMIT 1;";

        public const string UPSERT =
            $@"INSERT INTO inbox (ack_guid, entity_id, kind, consumer_id, def_id, def_version_id, instance_guid, on_success, on_failure, occurred, event_code, route, run_count)
               VALUES ({ACK_GUID}, lower(trim({ENTITY_ID})), {KIND}, {CONSUMER_ID}, {DEF_ID}, {DEF_VERSION_ID}, {INSTANCE_GUID}, {ON_SUCCESS}, {ON_FAILURE}, {OCCURRED}, {EVENT_CODE}, {ROUTE}, {RUN_COUNT})
               ON DUPLICATE KEY UPDATE id = LAST_INSERT_ID(id); SELECT LAST_INSERT_ID() AS id;";

        public const string UPDATE =
            $@"UPDATE inbox
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
            $@"SELECT * FROM inbox WHERE id = {WF_ID};";

        public const string GET_PINNED_HANDLER_VERSION =
            $@"SELECT handler_version FROM inbox
               WHERE def_id = {DEF_ID} AND entity_id = lower(trim({ENTITY_ID})) AND handler_version IS NOT NULL
               ORDER BY id ASC LIMIT 1;";

        public const string SET_HANDLER_VERSION =
            $@"UPDATE inbox SET handler_version = {HANDLER_VERSION}, handler_upgrade = {HANDLER_UPGRADE} WHERE id = {WF_ID};";

        public const string LIST_PAGED =
            $@"SELECT w.id, w.ack_guid, w.entity_id, w.kind, w.consumer_id, w.def_id, w.def_version_id,
              w.instance_guid, w.event_code, w.route, w.occurred, w.created,
              s.status AS inbox_status, s.attempt_count,
              o.status AS outbox_status, o.current_outcome, o.next_retry_at
       FROM inbox w
       LEFT JOIN inbox_status s ON s.inbox_id = w.id
       LEFT JOIN outbox o ON o.inbox_id = w.id
       ORDER BY w.id DESC
       LIMIT {TAKE} OFFSET {SKIP};";
    }
}
