using System.Net;
using System.Text.Json;
using Atlas.Infrastructure.Http.Abstractions;

namespace Atlas.Infrastructure.Http.Internal;

internal sealed class DefaultExternalApiErrorParser : IExternalApiErrorParser
{
    public ValueTask<ExternalApiError> ParseAsync(
        string providerName,
        HttpResponseMessage response,
        string? responseBody,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var code = response.StatusCode.ToString();
        var message = response.ReasonPhrase;
        var traceId = ReadHeader(response, "trace-id") ?? ReadHeader(response, "x-request-id");

        if (!string.IsNullOrWhiteSpace(responseBody))
        {
            TryReadJsonError(responseBody, ref code, ref message, ref traceId);
        }

        return ValueTask.FromResult(new ExternalApiError(
            providerName,
            code,
            message,
            response.StatusCode,
            traceId));
    }

    private static void TryReadJsonError(
        string responseBody,
        ref string? code,
        ref string? message,
        ref string? traceId)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return;

            code = ReadString(document.RootElement, "code")
                ?? ReadString(document.RootElement, "errorCode")
                ?? ReadString(document.RootElement, "error")
                ?? code;

            message = ReadString(document.RootElement, "message")
                ?? ReadString(document.RootElement, "error_description")
                ?? ReadString(document.RootElement, "detail")
                ?? message;

            traceId = ReadString(document.RootElement, "traceId")
                ?? ReadString(document.RootElement, "requestId")
                ?? traceId;
        }
        catch (JsonException)
        {
            message = string.IsNullOrWhiteSpace(message) ? Truncate(responseBody, 256) : message;
        }
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind is JsonValueKind.String or JsonValueKind.Number
                ? property.ToString()
                : null;
    }

    private static string? ReadHeader(HttpResponseMessage response, string headerName)
    {
        return response.Headers.TryGetValues(headerName, out var values)
            ? values.FirstOrDefault()
            : null;
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
