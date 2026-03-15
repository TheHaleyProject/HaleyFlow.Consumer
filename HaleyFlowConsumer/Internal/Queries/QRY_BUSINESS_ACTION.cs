using static Haley.Internal.QueryFields;

namespace Haley.Internal {
    internal static class QRY_BUSINESS_ACTION {
        public const string SELECT_ID_BY_KEY =
            $@"SELECT id FROM business_action
               WHERE instance_id = {INSTANCE_ID}
                 AND action_code = {ACTION_CODE}
               LIMIT 1;";

        public const string INSERT_IGNORE =
            $@"INSERT IGNORE INTO business_action (instance_id, action_code, status, started_at)
               VALUES ({INSTANCE_ID}, {ACTION_CODE}, {STATUS}, UTC_TIMESTAMP());";

        public const string SELECT_BY_ID =
            $@"SELECT * FROM business_action WHERE id = {ID} LIMIT 1;";

        public const string SELECT_BY_KEY =
            $@"SELECT * FROM business_action
               WHERE instance_id = {INSTANCE_ID}
                 AND action_code = {ACTION_CODE}
               LIMIT 1;";

        public const string SET_RUNNING =
            $@"UPDATE business_action
               SET status = {STATUS},
                   started_at = UTC_TIMESTAMP(),
                   completed_at = NULL,
                   last_error = NULL
               WHERE id = {ID};";

        public const string SET_COMPLETED =
            $@"UPDATE business_action
               SET status = {STATUS},
                   completed_at = UTC_TIMESTAMP(6),
                   result_json = {RESULT_JSON}
               WHERE id = {ID};";

        public const string SET_FAILED =
            $@"UPDATE business_action
               SET status = {STATUS},
                   completed_at = UTC_TIMESTAMP(6),
                   last_error = {LAST_ERROR}
               WHERE id = {ID};";
    }
}
