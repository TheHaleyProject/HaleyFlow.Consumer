using static Haley.Internal.QueryFields;

namespace Haley.Internal {
    internal static class QRY_INBOX_STEP {

        /// <summary>
        /// Upserts a step. Sets started_at when status transitions to Running (2),
        /// completed_at when Completed (3) or Failed (4). Preserves existing values for result/error if null supplied.
        /// </summary>
        public const string UPSERT =
            $@"INSERT INTO inbox_step (inbox_id, step_code, status, result_json, last_error)
               VALUES ({INBOX_ID}, {STEP_CODE}, {STATUS}, {RESULT_JSON}, {LAST_ERROR})
               ON DUPLICATE KEY UPDATE
                   status       = VALUES(status),
                   started_at   = CASE WHEN VALUES(status) = 2 AND started_at IS NULL THEN UTC_TIMESTAMP(6) ELSE started_at END,
                   completed_at = CASE WHEN VALUES(status) IN (3,4) THEN UTC_TIMESTAMP(6) ELSE completed_at END,
                   result_json  = COALESCE(VALUES(result_json), result_json),
                   last_error   = COALESCE(VALUES(last_error), last_error);";

        public const string SELECT_BY_INBOX_AND_CODE =
            $@"SELECT * FROM inbox_step WHERE inbox_id = {INBOX_ID} AND step_code = {STEP_CODE};";

        public const string SELECT_ALL_BY_INBOX =
            $@"SELECT * FROM inbox_step WHERE inbox_id = {INBOX_ID} ORDER BY step_code ASC;";
    }
}
