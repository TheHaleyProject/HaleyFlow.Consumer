using static Haley.Internal.QueryFields;

namespace Haley.Internal {
    internal static class QRY_TIMELINE {

        /// <summary>
        /// Consumer-side instance mirror row for the given engine instance GUID.
        /// Returned even when no inbox deliveries have been processed yet.
        /// </summary>
        public const string INSTANCE_BY_GUID =
            $@"SELECT
                  inst.id,
                  inst.guid,
                  inst.entity_guid,
                  inst.def_name,
                  inst.def_version_value AS def_version,
                  inst.created
               FROM instance inst
               WHERE inst.guid = {INSTANCE_GUID}
               LIMIT 1;";

        /// <summary>
        /// All inbox events for the given instance_guid, with their inbox_status and outbox rows
        /// (LEFT JOIN — rows without a status or outbox record are still returned).
        /// Also returns instance fields for timeline header population.
        /// Ordered oldest-first so the timeline renders chronologically.
        /// </summary>
        public const string EVENTS_BY_INSTANCE =
            $@"SELECT
                  inst.guid        AS instance_guid,
                  inst.entity_guid,
                  inst.def_name,
                  inst.def_version_value AS def_version,
                  i.id             AS inbox_id,
                  i.ack_guid,
                  i.kind,
                  i.handler_version,
                  i.event_code,
                  i.route,
                  i.dispatch_mode,
                  i.hook_type,
                  i.run_count,
                  i.occurred,
                  i.created,
                  s.status         AS inbox_status,
                  s.attempt_count,
                  s.last_error     AS inbox_error,
                  s.received_at,
                  s.modified       AS inbox_modified,
                  o.current_outcome,
                  o.status         AS outbox_status,
                  o.next_retry_at,
                  o.next_event,
                  o.last_error     AS outbox_error,
                  o.modified       AS outbox_modified
               FROM inbox i
               JOIN instance inst ON inst.id = i.instance_id
               LEFT JOIN inbox_status s ON s.inbox_id = i.id
               LEFT JOIN outbox o       ON o.inbox_id = i.id
               WHERE inst.guid = {INSTANCE_GUID}
               ORDER BY i.id ASC;";

        /// <summary>
        /// All inbox_action records (with joined business_action fields) for the given instance_guid.
        /// </summary>
        public const string ACTIONS_BY_INSTANCE =
            $@"SELECT
                  ia.inbox_id,
                  ia.action_id,
                  ia.status        AS inbox_action_status,
                  ia.last_error    AS inbox_action_error,
                  ba.action_code,
                  ba.status        AS business_status,
                  ba.started_at,
                  ba.completed_at,
                  ba.result_json
               FROM inbox_action ia
               JOIN business_action ba ON ba.id = ia.action_id
               JOIN inbox i ON i.id = ia.inbox_id
               JOIN instance inst ON inst.id = i.instance_id
               WHERE inst.guid = {INSTANCE_GUID}
               ORDER BY ia.inbox_id ASC, ia.action_id ASC;";

        /// <summary>
        /// All outbox ACK-attempt history rows for every inbox row belonging to the given instance_guid.
        /// </summary>
        public const string OUTBOX_HISTORY_BY_INSTANCE =
            $@"SELECT
                  oh.inbox_id,
                  oh.attempt_no,
                  oh.outcome,
                  oh.status,
                  oh.response,
                  oh.error,
                  oh.modified      AS created_at
               FROM outbox_history oh
               JOIN inbox i ON i.id = oh.inbox_id
               JOIN instance inst ON inst.id = i.instance_id
               WHERE inst.guid = {INSTANCE_GUID}
               ORDER BY oh.inbox_id ASC, oh.attempt_no ASC;";
    }
}
