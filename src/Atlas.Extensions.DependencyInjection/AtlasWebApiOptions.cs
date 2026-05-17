namespace Atlas.Extensions.DependencyInjection;

public sealed class AtlasWebApiOptions
{
    public string ApiTitle { get; set; } = "Atlas API";
    public string ApiVersion { get; set; } = "v1";
    public string CorsPolicyName { get; set; } = "AtlasDefaultCors";
    public bool EnableSwagger { get; set; } = true;
    public bool EnableHttpsRedirection { get; set; } = true;
}
