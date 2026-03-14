using static Haley.Internal.QueryFields;

namespace Haley.Internal {
    internal static class QRY_INBOX {

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
              i.entity_id, i.instance_guid, i.kind, i.route, i.event_code
       FROM inbox_status s
       JOIN inbox i ON i.id = s.inbox_id
       WHERE ({STATUS} < 0 OR s.status = {STATUS})
       ORDER BY s.modified DESC
       LIMIT {TAKE} OFFSET {SKIP};";

        public const string COUNT_PENDING =
            @"SELECT COUNT(*) FROM inbox_status WHERE status IN (1, 2);";
    }
}
