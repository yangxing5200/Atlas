using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Atlas.Infrastructure.Http.Abstractions;
using Atlas.Infrastructure.Http.Configuration;
using Atlas.Infrastructure.Http.Extensions;
using Atlas.Infrastructure.Http.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Http.Tests;

public sealed class ExternalHttpClientTests
{
    [Fact]
    public async Task GetAsync_Retries_Transient_Status_For_Idempotent_Request()
    {
        var handler = new SequenceHttpMessageHandler((_, call) =>
            call == 1
                ? Json(HttpStatusCode.ServiceUnavailable, new { code = "temporary_unavailable", message = "try again" })
                : Json(HttpStatusCode.OK, new TestPayload("ok")));

        using var provider = BuildProvider(handler, options =>
        {
            options.Resilience.Retry.MaxAttempts = 2;
            options.Resilience.Retry.BaseDelayMilliseconds = 1;
            options.Resilience.Retry.MaxDelayMilliseconds = 1;
            options.Resilience.Retry.UseJitter = false;
        });

        var client = provider.GetRequiredService<ITestExternalClient>();
        var result = await client.GetAsync("/products/1");

        Assert.Equal("ok", result.Value);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task PostAsync_Does_Not_Retry_Without_Idempotency_Signal()
    {
        var handler = new SequenceHttpMessageHandler((_, _) =>
            Json(HttpStatusCode.ServiceUnavailable, new { code = "temporary_unavailable", message = "try again" }));

        using var provider = BuildProvider(handler, options =>
        {
            options.Resilience.Retry.MaxAttempts = 3;
            options.Resilience.Retry.BaseDelayMilliseconds = 1;
            options.Resilience.Retry.MaxDelayMilliseconds = 1;
        });

        var client = provider.GetRequiredService<ITestExternalClient>();

        var ex = await Assert.ThrowsAsync<ExternalApiException>(() => client.PostAsync("/orders"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, ex.StatusCode);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task SendAsync_Maps_ThirdParty_Error_Response()
    {
        var handler = new SequenceHttpMessageHandler((_, _) =>
            Json(HttpStatusCode.BadRequest, new
            {
                code = "bad_request",
                message = "Bad input",
                traceId = "trace-001"
            }));

        using var provider = BuildProvider(handler);
        var client = provider.GetRequiredService<ITestExternalClient>();

        var ex = await Assert.ThrowsAsync<ExternalApiException>(() => client.GetAsync("/bad-request"));

        Assert.Equal("UnitTestApi", ex.ProviderName);
        Assert.Equal("bad_request", ex.ErrorCode);
        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Contains("Bad input", ex.Message, StringComparison.Ordinal);
        Assert.Contains("trace-001", ex.ResponseBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Client_Adds_Configured_Api_Key_Header()
    {
        var handler = new SequenceHttpMessageHandler((_, _) =>
            Json(HttpStatusCode.OK, new TestPayload("ok")));

        using var provider = BuildProvider(handler, options =>
        {
            options.Authentication.ApiKeyHeaderName = "X-Api-Key";
            options.Authentication.ApiKey = "secret-key";
        });

        var client = provider.GetRequiredService<ITestExternalClient>();
        await client.GetAsync("/headers");

        var request = Assert.Single(handler.Requests);
        Assert.True(request.Headers.TryGetValue("X-Api-Key", out var value));
        Assert.Equal("secret-key", value);
    }

    [Fact]
    public async Task CircuitBreaker_Opens_After_Configured_Failure_Threshold()
    {
        var handler = new SequenceHttpMessageHandler((_, _) =>
            Json(HttpStatusCode.ServiceUnavailable, new { code = "unavailable", message = "down" }));

        using var provider = BuildProvider(handler, options =>
        {
            options.Resilience.Retry.Enabled = false;
            options.Resilience.CircuitBreaker.Enabled = true;
            options.Resilience.CircuitBreaker.FailureThreshold = 1;
            options.Resilience.CircuitBreaker.BreakSeconds = 30;
        });

        var client = provider.GetRequiredService<ITestExternalClient>();
        await Assert.ThrowsAsync<ExternalApiException>(() => client.GetAsync("/down"));

        var ex = await Assert.ThrowsAsync<ExternalApiException>(() => client.GetAsync("/down-again"));

        Assert.Equal("circuit_open", ex.ErrorCode);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task LocalRateLimit_Rejects_Request_Before_Primary_Handler()
    {
        var handler = new SequenceHttpMessageHandler((_, _) =>
            Json(HttpStatusCode.OK, new TestPayload("ok")));

        using var provider = BuildProvider(handler, options =>
        {
            options.Resilience.RateLimit.Enabled = true;
            options.Resilience.RateLimit.PermitLimit = 1;
            options.Resilience.RateLimit.WindowSeconds = 30;
        });

        var client = provider.GetRequiredService<ITestExternalClient>();
        await client.GetAsync("/first");
        var ex = await Assert.ThrowsAsync<ExternalApiException>(() => client.GetAsync("/second"));

        Assert.Equal("local_rate_limited", ex.ErrorCode);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public void Redactor_Masks_Common_Secrets()
    {
        var redacted = ExternalHttpRedactor.RedactText("Authorization: Bearer secret-token, apiKey=my-key, password=pwd");

        Assert.DoesNotContain("secret-token", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("my-key", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("pwd", redacted, StringComparison.Ordinal);
        Assert.Contains("***", redacted, StringComparison.Ordinal);
    }

    private static ServiceProvider BuildProvider(
        HttpMessageHandler handler,
        Action<ExternalHttpClientOptions>? configure = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Atlas:Http:Clients:UnitTestApi:BaseUrl"] = "https://unit.test",
                ["Atlas:Http:Clients:UnitTestApi:Logging:Enabled"] = "false"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services
            .AddAtlasExternalHttpClient<ITestExternalClient, TestExternalClient>(
                configuration,
                "UnitTestApi",
                configure)
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        return services.BuildServiceProvider(validateScopes: true);
    }

    private static HttpResponseMessage Json(HttpStatusCode statusCode, object body)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = JsonContent.Create(body)
        };
    }

    private interface ITestExternalClient
    {
        Task<TestPayload> GetAsync(string path);

        Task<TestPayload> PostAsync(string path);
    }

    private sealed class TestExternalClient : ITestExternalClient
    {
        private readonly HttpClient _httpClient;
        private readonly IExternalApiExecutor _executor;

        public TestExternalClient(HttpClient httpClient, IExternalApiExecutor executor)
        {
            _httpClient = httpClient;
            _executor = executor;
        }

        public Task<TestPayload> GetAsync(string path)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            return _executor.SendAsync<TestPayload>(
                "UnitTestApi",
                _httpClient,
                request,
                new ExternalApiRequestOptions { IsIdempotent = true });
        }

        public Task<TestPayload> PostAsync(string path)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = JsonContent.Create(new { Value = "create" })
            };

            return _executor.SendAsync<TestPayload>(
                "UnitTestApi",
                _httpClient,
                request);
        }
    }

    private sealed record TestPayload(string Value);

    private sealed class SequenceHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, int, HttpResponseMessage> _responseFactory;
        private int _callCount;

        public SequenceHttpMessageHandler(Func<HttpRequestMessage, int, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public int CallCount => _callCount;

        public ConcurrentBag<RequestSnapshot> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var call = Interlocked.Increment(ref _callCount);
            Requests.Add(RequestSnapshot.From(request));
            return Task.FromResult(_responseFactory(request, call));
        }
    }

    private sealed record RequestSnapshot(IReadOnlyDictionary<string, string> Headers)
    {
        public static RequestSnapshot From(HttpRequestMessage request)
        {
            return new RequestSnapshot(request.Headers.ToDictionary(
                static header => header.Key,
                static header => string.Join(",", header.Value),
                StringComparer.OrdinalIgnoreCase));
        }
    }
}
