using static Haley.Internal.QueryFields;

namespace Haley.Internal {
    internal static class QRY_TIMELINE {

        /// <summary>
        /// All inbox events for the given instance_guid, with their inbox_status and outbox rows
        /// (LEFT JOIN — rows without a status or outbox record are still returned).
        /// Ordered oldest-first so the timeline renders chronologically.
        /// </summary>
        public const string EVENTS_BY_INSTANCE =
            $@"SELECT
                  i.id            AS inbox_id,
                  i.ack_guid,
                  i.entity_id,
                  i.kind,
                  i.def_id,
                  i.def_version_id,
                  i.handler_version,
                  i.event_code,
                  i.route,
                  i.run_count,
                  i.occurred,
                  i.created,
                  s.status        AS inbox_status,
                  s.attempt_count,
                  s.last_error    AS inbox_error,
                  s.received_at,
                  s.modified      AS inbox_modified,
                  o.current_outcome,
                  o.status        AS outbox_status,
                  o.next_retry_at,
                  o.last_error    AS outbox_error,
                  o.modified      AS outbox_modified
               FROM inbox i
               LEFT JOIN inbox_status s ON s.inbox_id = i.id
               LEFT JOIN outbox o       ON o.inbox_id = i.id
               WHERE i.instance_guid = {INSTANCE_GUID}
               ORDER BY i.id ASC;";

        /// <summary>
        /// All handler step records for every inbox row belonging to the given instance_guid.
        /// </summary>
        public const string STEPS_BY_INSTANCE =
            $@"SELECT
                  ist.inbox_id,
                  ist.action_code,
                  ist.status,
                  ist.started_at,
                  ist.completed_at,
                  ist.result_json,
                  ist.last_error
               FROM inbox_step ist
               JOIN inbox i ON i.id = ist.inbox_id
               WHERE i.instance_guid = {INSTANCE_GUID}
               ORDER BY ist.inbox_id ASC, ist.action_code ASC;";

        /// <summary>
        /// All outbox ACK-attempt history rows for every inbox row belonging to the given instance_guid.
        /// </summary>
        public const string OUTBOX_HISTORY_BY_INSTANCE =
            $@"SELECT
                  oh.outbox_id    AS inbox_id,
                  oh.attempt_no,
                  oh.outcome,
                  oh.status,
                  oh.response_payload_json,
                  oh.error,
                  oh.modified     AS created_at
               FROM outbox_history oh
               JOIN inbox i ON i.id = oh.outbox_id
               WHERE i.instance_guid = {INSTANCE_GUID}
               ORDER BY oh.outbox_id ASC, oh.attempt_no ASC;";
    }
}
