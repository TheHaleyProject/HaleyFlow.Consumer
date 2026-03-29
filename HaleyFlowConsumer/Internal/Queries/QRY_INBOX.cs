using static Haley.Internal.QueryFields;

namespace Haley.Internal {
    internal static class QRY_INBOX {
        public const string INSERT_IGNORE =
            $@"INSERT IGNORE INTO inbox (ack_guid, kind, instance_id, on_success, on_failure, occurred, event_code, route, run_count, dispatch_mode, hook_type)
               VALUES ({ACK_GUID}, {KIND}, {INSTANCE_ID}, {ON_SUCCESS}, {ON_FAILURE}, {OCCURRED}, {EVENT_CODE}, {ROUTE}, {RUN_COUNT}, {DISPATCH_MODE}, {HOOK_TYPE});";

        public const string SELECT_ID_BY_ACK_GUID =
            $@"SELECT id FROM inbox WHERE ack_guid = {ACK_GUID} LIMIT 1;";

        public const string SELECT_BY_ID =
            $@"SELECT * FROM inbox WHERE id = {INBOX_ROW_ID};";

        public const string GET_PINNED_HANDLER_VERSION =
            $@"SELECT handler_version FROM inbox
               WHERE instance_id = {INSTANCE_ID} AND handler_version IS NOT NULL
               ORDER BY id ASC LIMIT 1;";

        public const string SET_HANDLER_VERSION =
            $@"UPDATE inbox SET handler_version = {HANDLER_VERSION}, handler_upgrade = {HANDLER_UPGRADE} WHERE id = {INBOX_ROW_ID};";

        public const string LIST_PAGED =
            $@"SELECT w.id, w.ack_guid, w.kind, w.instance_id,
                      w.handler_version, w.handler_upgrade, w.on_success, w.on_failure,
                      w.occurred, w.event_code, w.route, w.run_count, w.created,
                      inst.guid AS instance_guid, inst.entity_guid, inst.def_name
               FROM inbox w
               JOIN instance inst ON inst.id = w.instance_id
               WHERE ({KIND} IS NULL OR w.kind = {KIND})
                 AND (NULLIF(TRIM({INSTANCE_GUID}), '') IS NULL OR inst.guid = trim({INSTANCE_GUID}))
                 AND (NULLIF(TRIM({ACK_GUID}), '') IS NULL OR w.ack_guid = trim({ACK_GUID}))
                 AND (NULLIF(TRIM({ROUTE}), '') IS NULL OR w.route = trim({ROUTE}))
                 AND ({EVENT_CODE} IS NULL OR w.event_code = {EVENT_CODE})
               ORDER BY w.id DESC
               LIMIT {TAKE} OFFSET {SKIP};";
    }
}
