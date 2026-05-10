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
