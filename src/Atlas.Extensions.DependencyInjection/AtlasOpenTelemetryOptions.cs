namespace Atlas.Extensions.DependencyInjection;

/// <summary>
/// OpenTelemetry registration options for Atlas runtime services.
/// </summary>
public sealed class AtlasOpenTelemetryOptions
{
    public const string SectionName = "Observability:OpenTelemetry";

    public bool Enabled { get; set; }
    public string ServiceName { get; set; } = "Atlas";
    public string? ServiceVersion { get; set; }
    public string Exporter { get; set; } = "None";
    public string OtlpEndpoint { get; set; } = "http://localhost:4317";
    public string OtlpProtocol { get; set; } = "Grpc";
    public bool InstrumentRuntime { get; set; } = true;
}
