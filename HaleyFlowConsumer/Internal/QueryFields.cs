namespace Haley.Internal {
    internal static class QueryFields {
        // shared
        public const string ID = "@ID";
        public const string GUID = "@GUID";
        public const string STATUS = "@STATUS";
        public const string LAST_ERROR = "@LAST_ERROR";
        // inbox
        public const string INBOX_ROW_ID = "@INBOX_ROW_ID";
        public const string ACK_GUID = "@ACK_GUID";
        public const string KIND = "@KIND";
        public const string HANDLER_VERSION = "@HANDLER_VERSION";
        public const string HANDLER_UPGRADE = "@HANDLER_UPGRADE";
        public const string INSTANCE_GUID = "@INSTANCE_GUID";
        public const string ON_SUCCESS = "@ON_SUCCESS";
        public const string ON_FAILURE = "@ON_FAILURE";
        public const string OCCURRED = "@OCCURRED";
        public const string EVENT_CODE = "@EVENT_CODE";
        public const string ROUTE = "@ROUTE";
        public const string RUN_COUNT = "@RUN_COUNT";
        public const string ACTION_CODE = "@ACTION_CODE";
        public const string ACTION_ID = "@ACTION_ID";
        // inbox_status / inbox_action / outbox FK to inbox.id
        public const string INBOX_ID = "@INBOX_ID";
        public const string PARAMS_JSON = "@PARAMS_JSON";
        // instance
        public const string DEF_NAME = "@DEF_NAME";
        public const string DEF_VERSION = "@DEF_VERSION";
        public const string ENTITY_GUID = "@ENTITY_GUID";
        public const string INSTANCE_ID = "@INSTANCE_ID";
        // business_action
        public const string RESULT_JSON = "@RESULT_JSON";
        // outbox
        public const string OUTCOME = "@OUTCOME";
        public const string NEXT_RETRY_AT = "@NEXT_RETRY_AT";
        public const string RESPONSE_PAYLOAD = "@RESPONSE_PAYLOAD";
        public const string ERROR = "@ERROR";
        public const string ATTEMPT_NO = "@ATTEMPT_NO";
        // paging
        public const string TAKE = "@TAKE";
        public const string SKIP = "@SKIP";
    }
}
