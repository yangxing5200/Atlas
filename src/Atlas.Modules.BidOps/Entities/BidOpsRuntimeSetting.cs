namespace Atlas.Modules.BidOps.Entities;

/// <summary>
/// BidOps 租户级运行配置。
/// </summary>
public sealed class BidOpsRuntimeSetting : BidOpsTenantEntity
{
    /// <summary>
    /// 运行配置键。
    /// </summary>
    public string SettingKey { get; set; } = string.Empty;

    /// <summary>
    /// 运行配置值。
    /// </summary>
    public string SettingValue { get; set; } = string.Empty;

    /// <summary>
    /// 最近更新该配置的用户名。
    /// </summary>
    public string UpdatedByUserName { get; set; } = string.Empty;
}
