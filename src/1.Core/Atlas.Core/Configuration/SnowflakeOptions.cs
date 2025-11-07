namespace Atlas.Core.Configuration;

/// <summary>
/// Snowflake 配置选项
/// </summary>
public class SnowflakeOptions
{
    /// <summary>
    /// 机器ID (0-31)
    /// </summary>
    public long WorkerId { get; set; }

    /// <summary>
    /// 数据中心ID (0-31)
    /// </summary>
    public long DatacenterId { get; set; }

    /// <summary>
    /// 验证配置
    /// </summary>
    public void Validate()
    {
        if (WorkerId < 0 || WorkerId > 31)
            throw new ArgumentException("WorkerId 必须在 0-31 之间");

        if (DatacenterId < 0 || DatacenterId > 31)
            throw new ArgumentException("DatacenterId 必须在 0-31 之间");
    }
}