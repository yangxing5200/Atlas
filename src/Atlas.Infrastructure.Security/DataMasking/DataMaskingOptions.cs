namespace Atlas.Infrastructure.Security.DataMasking;

public sealed class DataMaskingOptions
{
    public const string SectionName = "DataMasking";

    /// <summary>
    /// Global switch for automatic API response masking. Enabled by default.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
