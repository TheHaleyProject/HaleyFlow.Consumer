using static Haley.Internal.QueryFields;

namespace Haley.Internal {
    internal static class QRY_INBOX_STATUS {

        public const string UPSERT =
            $@"INSERT IGNORE INTO inbox_status (inbox_id, params_json)
               VALUES ({INBOX_ID}, {PARAMS_JSON});";

        public const string SET_STATUS =
            $@"UPDATE inbox_status SET status = {STATUS}, last_error = {LAST_ERROR}, modified = UTC_TIMESTAMP()
               WHERE inbox_id = {INBOX_ID};";

        public const string INCREMENT_ATTEMPT =
            $@"UPDATE inbox_status SET attempt_count = attempt_count + 1, modified = UTC_TIMESTAMP()
               WHERE inbox_id = {INBOX_ID};";

        public const string SELECT_BY_INBOX_ID =
            $@"SELECT * FROM inbox_status WHERE inbox_id = {INBOX_ID};";

        public const string LIST_PAGED =
            $@"SELECT s.inbox_id, s.status, s.attempt_count, s.last_error, s.received_at, s.modified,
              i.ack_guid, i.entity_id, i.instance_guid, i.kind, i.def_id, i.def_version_id,
              i.route, i.event_code, i.occurred, i.created
       FROM inbox_status s
       JOIN inbox i ON i.id = s.inbox_id
       WHERE ({STATUS} IS NULL OR s.status = {STATUS})
         AND ({KIND} IS NULL OR i.kind = {KIND})
         AND ({DEF_ID} IS NULL OR i.def_id = {DEF_ID})
         AND ({DEF_VERSION_ID} IS NULL OR i.def_version_id = {DEF_VERSION_ID})
         AND (NULLIF(TRIM({ENTITY_ID}), '') IS NULL OR i.entity_id = lower(trim({ENTITY_ID})))
         AND (NULLIF(TRIM({INSTANCE_GUID}), '') IS NULL OR i.instance_guid = trim({INSTANCE_GUID}))
         AND (NULLIF(TRIM({ACK_GUID}), '') IS NULL OR i.ack_guid = trim({ACK_GUID}))
         AND (NULLIF(TRIM({ROUTE}), '') IS NULL OR i.route = trim({ROUTE}))
         AND ({EVENT_CODE} IS NULL OR i.event_code = {EVENT_CODE})
       ORDER BY s.modified DESC
       LIMIT {TAKE} OFFSET {SKIP};";

        public const string COUNT_PENDING =
            @"SELECT COUNT(*) FROM inbox_status WHERE status IN (1, 2);";
    }
}
