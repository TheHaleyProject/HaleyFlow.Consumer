using static Haley.Internal.QueryFields;

namespace Haley.Internal {
    internal static class QRY_INBOX_ACTION {
        /// <summary>
        /// Records that a business action was attempted for a specific inbox delivery.
        /// On duplicate updates status and preserves last_error if a new one isn't supplied.
        /// </summary>
        public const string UPSERT =
            $@"INSERT INTO inbox_action (inbox_id, action_id, status, last_error)
               VALUES ({INBOX_ID}, {ACTION_ID}, {STATUS}, {LAST_ERROR})
               ON DUPLICATE KEY UPDATE
                   status     = VALUES(status),
                   last_error = COALESCE(VALUES(last_error), last_error);";

        public const string SELECT_BY_INBOX_AND_ACTION =
            $@"SELECT * FROM inbox_action WHERE inbox_id = {INBOX_ID} AND action_id = {ACTION_ID};";

        public const string SELECT_ALL_BY_INBOX =
            $@"SELECT ia.*, ba.action_code, ba.started_at, ba.completed_at, ba.result_json, ba.status AS business_status
               FROM inbox_action ia
               JOIN business_action ba ON ba.id = ia.action_id
               WHERE ia.inbox_id = {INBOX_ID}
               ORDER BY ia.action_id ASC;";
    }
}
