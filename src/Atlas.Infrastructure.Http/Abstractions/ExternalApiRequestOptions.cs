namespace Atlas.Infrastructure.Http.Abstractions;

public sealed class ExternalApiRequestOptions
{
    public string? OperationName { get; set; }

    public bool? IsIdempotent { get; set; }

    public Dictionary<string, string> Headers { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}
