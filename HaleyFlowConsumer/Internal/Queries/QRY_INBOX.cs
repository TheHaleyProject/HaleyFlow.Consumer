using static Haley.Internal.QueryFields;

namespace Haley.Internal {
    internal static class QRY_INBOX {
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
              w.handler_version, w.handler_upgrade, w.instance_guid, w.on_success, w.on_failure,
              w.occurred, w.event_code, w.route, w.run_count, w.created
       FROM inbox w
       WHERE ({KIND} IS NULL OR w.kind = {KIND})
         AND ({DEF_ID} IS NULL OR w.def_id = {DEF_ID})
         AND ({DEF_VERSION_ID} IS NULL OR w.def_version_id = {DEF_VERSION_ID})
         AND (NULLIF(TRIM({ENTITY_ID}), '') IS NULL OR w.entity_id = lower(trim({ENTITY_ID})))
         AND (NULLIF(TRIM({INSTANCE_GUID}), '') IS NULL OR w.instance_guid = trim({INSTANCE_GUID}))
         AND (NULLIF(TRIM({ACK_GUID}), '') IS NULL OR w.ack_guid = trim({ACK_GUID}))
         AND (NULLIF(TRIM({ROUTE}), '') IS NULL OR w.route = trim({ROUTE}))
         AND ({EVENT_CODE} IS NULL OR w.event_code = {EVENT_CODE})
       ORDER BY w.id DESC
       LIMIT {TAKE} OFFSET {SKIP};";
    }
}
