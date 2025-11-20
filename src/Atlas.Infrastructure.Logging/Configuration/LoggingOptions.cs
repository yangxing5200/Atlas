namespace Atlas.Infrastructure.Logging.Configuration
{
    /// <summary>
    /// 日志配置选项
    /// </summary>
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
        /// 日志文件保留天数
        /// </summary>
        public int RetainedFileCountLimit { get; set; } = 30;

        /// <summary>
        /// 错误日志保留天数
        /// </summary>
        public int ErrorRetainedFileCountLimit { get; set; } = 90;

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
        /// 最小日志级别
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
    }
}