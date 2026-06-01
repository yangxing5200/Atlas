namespace Atlas.Core.Logging
{
    /// <summary>
    /// 日志上下文键常量
    /// </summary>
    public static class LogContextKeys
    {
        // 基础上下文
        public const string CorrelationId = "CorrelationId";
        public const string OperationId = "OperationId";
        public const string TraceId = "TraceId";
        public const string SpanId = "SpanId";
        public const string RequestPath = "RequestPath";
        public const string RequestMethod = "RequestMethod";

        // 租户上下文
        public const string TenantId = "TenantId";
        public const string TenantCode = "TenantCode";
        public const string StoreId = "StoreId";
        public const string StoreCode = "StoreCode";
        public const string UserId = "UserId";
        public const string UserName = "UserName";

        // 性能指标
        public const string ElapsedMilliseconds = "ElapsedMs";
        public const string Operation = "Operation";
        public const string EntityType = "EntityType";
        public const string EntityId = "EntityId";

        // 业务上下文
        public const string OrderId = "OrderId";
        public const string PatientId = "PatientId";
        public const string AppointmentId = "AppointmentId";
    }
}
