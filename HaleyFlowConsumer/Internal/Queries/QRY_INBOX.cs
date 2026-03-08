using static Haley.Internal.QueryFields;

namespace Haley.Internal {
    internal static class QRY_INBOX {

        public const string UPSERT =
            $@"INSERT IGNORE INTO inbox (wf_id, params_json)
               VALUES ({WF_ID}, {PARAMS_JSON});";

        public const string SET_STATUS =
            $@"UPDATE inbox SET status = {STATUS}, last_error = {LAST_ERROR}, modified = UTC_TIMESTAMP()
               WHERE wf_id = {WF_ID};";

        public const string INCREMENT_ATTEMPT =
            $@"UPDATE inbox SET attempt_count = attempt_count + 1, modified = UTC_TIMESTAMP()
               WHERE wf_id = {WF_ID};";

        public const string SELECT_BY_WF_ID =
            $@"SELECT * FROM inbox WHERE wf_id = {WF_ID};";

        public const string LIST_PAGED =
            $@"SELECT i.wf_id, i.status, i.attempt_count, i.last_error, i.received_at, i.modified,
              w.entity_id, w.instance_guid, w.kind, w.route, w.event_code
       FROM inbox i
       JOIN workflow w ON w.id = i.wf_id
       WHERE ({STATUS} < 0 OR i.status = {STATUS})
       ORDER BY i.modified DESC
       LIMIT {TAKE} OFFSET {SKIP};";

        public const string COUNT_PENDING =
            @"SELECT COUNT(*) FROM inbox WHERE status IN (1, 2);";
    }
}
