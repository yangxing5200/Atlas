using System.Net;

namespace Atlas.Infrastructure.Http.Abstractions;

public sealed record ExternalApiError(
    string ProviderName,
    string? Code,
    string? Message,
    HttpStatusCode? StatusCode,
    string? TraceId);
