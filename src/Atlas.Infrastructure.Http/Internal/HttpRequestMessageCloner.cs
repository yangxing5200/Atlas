namespace Atlas.Infrastructure.Http.Internal;

internal static class HttpRequestMessageCloner
{
    public static async Task<BufferedHttpRequest> BufferAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        byte[]? contentBytes = null;
        List<KeyValuePair<string, IEnumerable<string>>>? contentHeaders = null;

        if (request.Content is not null)
        {
            contentBytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            contentHeaders = request.Content.Headers
                .Select(static header => new KeyValuePair<string, IEnumerable<string>>(header.Key, header.Value.ToArray()))
                .ToList();
        }

        return new BufferedHttpRequest(contentBytes, contentHeaders);
    }

    public static HttpRequestMessage Clone(HttpRequestMessage request, BufferedHttpRequest buffered)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy
        };

        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        foreach (var option in request.Options)
            clone.Options.Set(new HttpRequestOptionsKey<object?>(option.Key), option.Value);

        if (buffered.ContentBytes is not null)
        {
            clone.Content = new ByteArrayContent(buffered.ContentBytes);

            if (buffered.ContentHeaders is not null)
            {
                foreach (var header in buffered.ContentHeaders)
                    clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
    }
}

internal sealed record BufferedHttpRequest(
    byte[]? ContentBytes,
    IReadOnlyList<KeyValuePair<string, IEnumerable<string>>>? ContentHeaders);
