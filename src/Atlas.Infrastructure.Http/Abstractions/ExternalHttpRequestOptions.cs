namespace Atlas.Infrastructure.Http.Abstractions;

public static class ExternalHttpRequestOptions
{
    public static readonly HttpRequestOptionsKey<string> ProviderName = new("Atlas.ExternalHttp.ProviderName");

    public static readonly HttpRequestOptionsKey<string> OperationName = new("Atlas.ExternalHttp.OperationName");

    public static readonly HttpRequestOptionsKey<bool> IsIdempotent = new("Atlas.ExternalHttp.IsIdempotent");
}
