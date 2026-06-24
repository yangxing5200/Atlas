namespace Atlas.Modules.BidOps.Entities;

public sealed class BidOpsRuntimeSetting : BidOpsTenantEntity
{
    public string SettingKey { get; set; } = string.Empty;

    public string SettingValue { get; set; } = string.Empty;

    public string UpdatedByUserName { get; set; } = string.Empty;
}
