using System.Diagnostics;

namespace Atlas.Core.Telemetry;

/// <summary>
/// Shared telemetry names used by framework components.
/// </summary>
public static class AtlasTelemetry
{
    public const string ActivitySourceName = "Atlas";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
