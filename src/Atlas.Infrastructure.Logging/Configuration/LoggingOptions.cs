namespace Atlas.Infrastructure.Logging.Configuration
{
    /// <summary>
    /// 日志配置选项
    /// </summary>
    /// <remarks>
    /// 配置路径为 Logging:Atlas。文件输出按日滚动，审计日志保留周期默认长于应用日志。
    /// </remarks>
    public class LoggingOptions
    {
        public const string SectionName = "Logging:Atlas";

        /// <summary>
        /// 是否启用控制台输出
        /// </summary>
        public bool EnableConsole { get; set; } = true;

        /// <summary>
        /// 是否启用文件输出
        /// </summary>
        public bool EnableFile { get; set; } = true;

        /// <summary>
        /// 文件日志路径
        /// </summary>
        public string FilePath { get; set; } = "logs/application/atlas-.log";

        /// <summary>
        /// 错误日志路径
        /// </summary>
        public string ErrorFilePath { get; set; } = "logs/application/atlas-errors-.log";

        /// <summary>
        /// 审计日志路径
        /// </summary>
        public string AuditFilePath { get; set; } = "logs/audit/audit-.log";

        /// <summary>
        /// 日志文件保留数量
        /// </summary>
        public int RetainedFileCount { get; set; } = 30;

        /// <summary>
        /// 错误日志保留数量
        /// </summary>
        public int ErrorRetainedFileCount { get; set; } = 90;

        /// <summary>
        /// 审计日志保留数量
        /// </summary>
        public int AuditRetainedFileCount { get; set; } = 365;

        /// <summary>
        /// 是否启用 Seq
        /// </summary>
        public bool EnableSeq { get; set; } = false;

        /// <summary>
        /// Seq 服务器地址
        /// </summary>
        public string? SeqServerUrl { get; set; }

        /// <summary>
        /// Seq API Key
        /// </summary>
        public string? SeqApiKey { get; set; }

        /// <summary>
        /// 是否启用敏感数据过滤
        /// </summary>
        public bool EnableSensitiveDataFilter { get; set; } = true;

        /// <summary>
        /// 敏感字段列表
        /// </summary>
        public List<string> SensitiveFields { get; set; } = new()
        {
            "password", "pwd", "secret", "token", "apikey",
            "idcard", "idnumber", "phone", "mobile", "email",
            "bankcard", "creditcard"
        };

        /// <summary>
        /// 最小日志级别 (Information/Debug/Warning/Error)
        /// </summary>
        public string MinimumLevel { get; set; } = "Information";

        /// <summary>
        /// 是否记录请求体
        /// </summary>
        public bool LogRequestBody { get; set; } = false;

        /// <summary>
        /// 是否记录响应体
        /// </summary>
        public bool LogResponseBody { get; set; } = false;

        /// <summary>
        /// 慢操作阈值（毫秒）
        /// </summary>
        public int SlowOperationThresholdMs { get; set; } = 1000;

        /// <summary>
        /// 请求体最大记录长度
        /// </summary>
        public int MaxRequestBodyLength { get; set; } = 4096;

        /// <summary>
        /// 响应体最大记录长度
        /// </summary>
        public int MaxResponseBodyLength { get; set; } = 4096;
    }
}
