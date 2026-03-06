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
    }
}
