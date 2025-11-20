namespace Atlas.Core.Logging
{
    /// <summary>
    /// 日志上下文接口
    /// </summary>
    public interface ILogContext
    {
        /// <summary>
        /// 关联ID（用于追踪完整请求链路）
        /// </summary>
        string CorrelationId { get; }

        /// <summary>
        /// 操作ID（用于标识当前操作）
        /// </summary>
        string OperationId { get; }

        /// <summary>
        /// 添加日志属性
        /// </summary>
        IDisposable PushProperty(string name, object value);

        /// <summary>
        /// 批量添加日志属性
        /// </summary>
        IDisposable PushProperties(IDictionary<string, object> properties);
    }
}