using System.Net;
using System.Net.Http.Json;
using Agentor.Contracts.Conexus;
using Agentor.Infrastructure.Conexus;
using Agentor.Infrastructure.Http;
using Agentor.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Agentor.Infrastructure.Tests;

public sealed class HttpModelGatewayClientTests
{
    [Fact]
    public async Task CompleteAsync_PostsToV1ModelComplete()
    {
        var requestDto = new ModelCallRequestDto("hi", "m1");
        var resultDto = new ModelCallResultDto("ok", "p", "m1", 1, 2, 0m, 3);

        var handler = new LambdaHandler((req, _) =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.EndsWith("/v1/model/complete", req.RequestUri!.AbsolutePath, StringComparison.Ordinal);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(resultDto, options: AgentorHttpJson.Options),
            });
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://conexus.test/") };

        var opts = new AgentorIntegrationsOptions
        {
            Conexus = new IntegrationFamilyOptions
            {
                Mode = IntegrationAdapterMode.Http,
                Http = new HttpIntegrationOptions { BaseUrl = "http://conexus.test/" },
            },
        };

        var sut = new HttpModelGatewayClient(new StubFactory(httpClient), new StaticMonitor(opts));

        var result = await sut.CompleteAsync(requestDto, CancellationToken.None);

        Assert.Equal("ok", result.CompletionText);
        Assert.Equal("m1", result.ModelId);
    }

    [Fact]
    public async Task CompleteAsync_deserializes_full_telemetry_and_profile_refs()
    {
        var requestDto = new ModelCallRequestDto("hi", "m1", "pp", "mp");
        var resultDto = new ModelCallResultDto("ok", "prov", "m1", 10, 20, 0.03m, 42, "pp", "mp");

        var handler = new LambdaHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(resultDto, options: AgentorHttpJson.Options),
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://conexus.test/") };
        var opts = new AgentorIntegrationsOptions
        {
            Conexus = new IntegrationFamilyOptions
            {
                Mode = IntegrationAdapterMode.Http,
                Http = new HttpIntegrationOptions { BaseUrl = "http://conexus.test/" },
            },
        };

        var sut = new HttpModelGatewayClient(new StubFactory(httpClient), new StaticMonitor(opts));

        var result = await sut.CompleteAsync(requestDto, CancellationToken.None);

        Assert.Equal(10, result.PromptTokens);
        Assert.Equal(20, result.CompletionTokens);
        Assert.Equal(0.03m, result.EstimatedCostUnits);
        Assert.Equal(42, result.LatencyMs);
        Assert.Equal("pp", result.PromptProfileRef);
        Assert.Equal("mp", result.ModelProfileRef);
    }

    [Fact]
    public async Task CompleteAsync_posts_declared_budget_json_when_set_on_request()
    {
        var requestDto = new ModelCallRequestDto("p", "mid", null, null, 1.25m, 500);

        ModelCallRequestDto? parsed = null;
        var handler = new LambdaHandler(async (req, _) =>
        {
            parsed = await req.Content!.ReadFromJsonAsync<ModelCallRequestDto>(AgentorHttpJson.Options);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(
                    new ModelCallResultDto("x", "p", "mid", 1, 1, 0m, 1),
                    options: AgentorHttpJson.Options),
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://conexus.test/") };
        var opts = new AgentorIntegrationsOptions
        {
            Conexus = new IntegrationFamilyOptions
            {
                Mode = IntegrationAdapterMode.Http,
                Http = new HttpIntegrationOptions { BaseUrl = "http://conexus.test/" },
            },
        };

        var sut = new HttpModelGatewayClient(new StubFactory(httpClient), new StaticMonitor(opts));

        _ = await sut.CompleteAsync(requestDto, CancellationToken.None);

        Assert.NotNull(parsed);
        Assert.Equal(1.25m, parsed!.DeclaredCostUnits);
        Assert.Equal(500, parsed.DeclaredLatencyMs);
    }

    [Fact]
    public async Task CompleteAsync_on5xx_throws_HttpRequestException()
    {
        var handler = new LambdaHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("overload"),
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://conexus.test/") };
        var opts = new AgentorIntegrationsOptions
        {
            Conexus = new IntegrationFamilyOptions
            {
                Mode = IntegrationAdapterMode.Http,
                Http = new HttpIntegrationOptions { BaseUrl = "http://conexus.test/" },
            },
        };

        var sut = new HttpModelGatewayClient(new StubFactory(httpClient), new StaticMonitor(opts));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            sut.CompleteAsync(new ModelCallRequestDto("a", "b"), CancellationToken.None));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, ex.StatusCode);
        Assert.Contains("503", ex.Message, StringComparison.Ordinal);
        Assert.Contains("overload", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompleteAsync_short_client_timeout_maps_to_TaskCanceledException()
    {
        var handler = new LambdaHandler(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10), ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://conexus.test/"), Timeout = TimeSpan.FromMilliseconds(30) };
        var opts = new AgentorIntegrationsOptions
        {
            Conexus = new IntegrationFamilyOptions
            {
                Mode = IntegrationAdapterMode.Http,
                Http = new HttpIntegrationOptions { BaseUrl = "http://conexus.test/" },
            },
        };

        var sut = new HttpModelGatewayClient(new StubFactory(httpClient), new StaticMonitor(opts));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            sut.CompleteAsync(new ModelCallRequestDto("a", "b"), CancellationToken.None));
    }

    private sealed class StubFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StaticMonitor(AgentorIntegrationsOptions value) : IOptionsMonitor<AgentorIntegrationsOptions>
    {
        public AgentorIntegrationsOptions CurrentValue => value;

        public AgentorIntegrationsOptions Get(string? name) => value;

        public IDisposable OnChange(Action<AgentorIntegrationsOptions, string?> listener) =>
            new EmptyDisposable();

        private sealed class EmptyDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    private sealed class LambdaHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}
