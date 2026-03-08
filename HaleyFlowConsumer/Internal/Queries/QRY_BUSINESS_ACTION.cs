using static Haley.Internal.QueryFields;

namespace Haley.Internal {
    internal static class QRY_BUSINESS_ACTION {
        public const string UPSERT_RETURN_ID =
            $@"INSERT INTO business_action (consumer_id, def_id, entity_id, action_code, status)
               VALUES ({CONSUMER_ID}, {DEF_ID}, lower(trim({ENTITY_ID})), {ACTION_CODE}, {STATUS})
               ON DUPLICATE KEY UPDATE id = LAST_INSERT_ID(id);
               SELECT LAST_INSERT_ID() AS id;";

        public const string SELECT_BY_ID =
            $@"SELECT * FROM business_action WHERE id = {ID} LIMIT 1;";

        public const string SELECT_BY_KEY =
            $@"SELECT * FROM business_action
               WHERE consumer_id = {CONSUMER_ID}
                 AND def_id = {DEF_ID}
                 AND entity_id = lower(trim({ENTITY_ID}))
                 AND action_code = {ACTION_CODE}
               LIMIT 1;";

        public const string SET_RUNNING =
            $@"UPDATE business_action
               SET status = {STATUS},
                   started_at = UTC_TIMESTAMP(),
                   completed_at = NULL
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
                   result_json = {RESULT_JSON}
               WHERE id = {ID};";
    }
}
