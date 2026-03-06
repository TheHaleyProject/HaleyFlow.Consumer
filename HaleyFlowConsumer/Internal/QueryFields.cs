namespace Haley.Internal {
    internal static class QueryFields {
        // workflow
        public const string WF_ID = "@WF_ID";
        public const string ACK_GUID = "@ACK_GUID";
        public const string ENTITY_ID = "@ENTITY_ID";
        public const string KIND = "@KIND";
        public const string CONSUMER_ID = "@CONSUMER_ID";
        public const string DEF_ID = "@DEF_ID";
        public const string DEF_VERSION_ID = "@DEF_VERSION_ID";
        public const string HANDLER_VERSION = "@HANDLER_VERSION";
        public const string HANDLER_UPGRADE = "@HANDLER_UPGRADE";
        public const string INSTANCE_GUID = "@INSTANCE_GUID";
        public const string ON_SUCCESS = "@ON_SUCCESS";
        public const string ON_FAILURE = "@ON_FAILURE";
        public const string OCCURRED = "@OCCURRED";
        public const string EVENT_CODE = "@EVENT_CODE";
        public const string ROUTE = "@ROUTE";
        // inbox
        public const string PARAMS_JSON = "@PARAMS_JSON";
        public const string STATUS = "@STATUS";
        public const string LAST_ERROR = "@LAST_ERROR";
        // inbox_step
        public const string INBOX_ID = "@INBOX_ID";
        public const string STEP_CODE = "@STEP_CODE";
        public const string RESULT_JSON = "@RESULT_JSON";
        // outbox
        public const string OUTCOME = "@OUTCOME";
        public const string NEXT_RETRY_AT = "@NEXT_RETRY_AT";
        public const string RESPONSE_PAYLOAD = "@RESPONSE_PAYLOAD";
        public const string ERROR = "@ERROR";
        public const string ATTEMPT_NO = "@ATTEMPT_NO";
        // paging
        public const string TAKE = "@TAKE";
    }
}
