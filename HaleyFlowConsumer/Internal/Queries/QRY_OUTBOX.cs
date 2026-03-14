using static Haley.Internal.QueryFields;

namespace Haley.Internal {
    internal static class QRY_OUTBOX {

        /// <summary>
        /// Creates outbox row if not exists. On duplicate resets to Pending with latest outcome
        /// (e.g. if a prior attempt failed and we want to retry with a new outcome decision).
        /// </summary>
        public const string UPSERT =
            $@"INSERT INTO outbox (inbox_id, current_outcome, status)
               VALUES ({INBOX_ID}, {OUTCOME}, 1)
               ON DUPLICATE KEY UPDATE
                   current_outcome = VALUES(current_outcome),
                   status          = 1,
                   next_retry_at   = NULL,
                   last_error      = NULL,
                   modified        = UTC_TIMESTAMP();";

        public const string SET_STATUS =
            $@"UPDATE outbox SET status = {STATUS}, last_error = {LAST_ERROR}, next_retry_at = {NEXT_RETRY_AT}, modified = UTC_TIMESTAMP()
               WHERE inbox_id = {INBOX_ID};";

        /// <summary>
        /// Lists pending outbox rows that are due, joined with inbox for ack_guid.
        /// Also calculates the next attempt number inline.
        /// </summary>
        public const string LIST_DUE_PENDING =
            $@"SELECT o.inbox_id, o.current_outcome, o.status, o.next_retry_at,
                      i.ack_guid, i.consumer_id,
                      COALESCE((SELECT MAX(oh.attempt_no) FROM outbox_history oh WHERE oh.outbox_id = o.inbox_id), 0) AS last_attempt_no
               FROM outbox o
               JOIN inbox i ON i.id = o.inbox_id
               WHERE o.status = 1
                 AND (o.next_retry_at IS NULL OR o.next_retry_at <= UTC_TIMESTAMP())
               ORDER BY o.modified ASC
               LIMIT {TAKE};";

        public const string ADD_HISTORY =
            $@"INSERT INTO outbox_history (outbox_id, outcome, status, attempt_no, response_payload_json, error)
               VALUES ({INBOX_ID}, {OUTCOME}, {STATUS},
                       COALESCE((SELECT MAX(attempt_no) FROM outbox_history WHERE outbox_id = {INBOX_ID}), 0) + 1,
                       {RESPONSE_PAYLOAD}, {ERROR});";

        public const string LIST_PAGED =
            $@"SELECT o.inbox_id, o.current_outcome, o.status, o.next_retry_at, o.last_error, o.modified,
              i.entity_id, i.instance_guid, i.kind, i.route, i.event_code
       FROM outbox o
       JOIN inbox i ON i.id = o.inbox_id
       WHERE ({STATUS} < 0 OR o.status = {STATUS})
       ORDER BY o.modified DESC
       LIMIT {TAKE} OFFSET {SKIP};";

        public const string COUNT_PENDING =
            @"SELECT COUNT(*) FROM outbox WHERE status = 1;";
    }
}
