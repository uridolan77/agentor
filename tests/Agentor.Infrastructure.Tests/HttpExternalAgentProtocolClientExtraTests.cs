using System.Net;
using System.Net.Http.Json;
using Agentor.Contracts.ExternalAgents;
using Agentor.Infrastructure.ExternalAgents;
using Agentor.Infrastructure.Http;
using Agentor.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Agentor.Infrastructure.Tests;

public sealed class HttpExternalAgentProtocolClientExtraTests
{
    [Fact]
    public async Task ListCapabilitiesAsync_GetsV1CapabilitiesQuery()
    {
        var caps = new List<ExternalAgentCapabilityDto>
        {
            new(ExternalAgentProtocolKind.A2AStyled, "a", "c", "s"),
        };

        var handler = new LambdaHandler((req, _) =>
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.Contains("/v1/capabilities", req.RequestUri!.AbsolutePath, StringComparison.Ordinal);
            Assert.Contains("protocolKind=", req.RequestUri!.Query, StringComparison.Ordinal);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(caps, options: AgentorHttpJson.Options),
            });
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://ext.test/") };

        var opts = new AgentorIntegrationsOptions
        {
            ExternalAgents = new IntegrationFamilyOptions
            {
                Mode = IntegrationAdapterMode.Http,
                Http = new HttpIntegrationOptions { BaseUrl = "http://ext.test/" },
            },
        };

        var sut = new HttpExternalAgentProtocolClient(new StubFactory(httpClient), new StaticMonitor(opts));

        var list = await sut.ListCapabilitiesAsync(ExternalAgentProtocolKind.A2AStyled);

        Assert.Single(list);
        Assert.Equal("a", list[0].AgentKey);
    }

    [Fact]
    public async Task InvokeAsync_PostsV1Invocations()
    {
        var body = new ExternalAgentInvocationRequestDto(
            ExternalAgentProtocolKind.A2AStyled,
            "ag",
            "cap",
            new Dictionary<string, string>());

        var responseBody = new ExternalAgentInvocationResultDto(
            ExternalAgentInvocationStatus.Succeeded,
            new Dictionary<string, string>(),
            null,
            true);

        var handler = new LambdaHandler((req, _) =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.EndsWith("/v1/invocations", req.RequestUri!.AbsolutePath, StringComparison.Ordinal);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(responseBody, options: AgentorHttpJson.Options),
            });
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://ext.test/") };

        var opts = new AgentorIntegrationsOptions
        {
            ExternalAgents = new IntegrationFamilyOptions
            {
                Mode = IntegrationAdapterMode.Http,
                Http = new HttpIntegrationOptions { BaseUrl = "http://ext.test/" },
            },
        };

        var sut = new HttpExternalAgentProtocolClient(new StubFactory(httpClient), new StaticMonitor(opts));

        var result = await sut.InvokeAsync(body, CancellationToken.None);

        Assert.Equal(ExternalAgentInvocationStatus.Succeeded, result.Status);
    }

    [Fact]
    public async Task ListCapabilitiesAsync_on4xx_throws_HttpRequestException()
    {
        var handler = new LambdaHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent("no"),
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://ext.test/") };

        var opts = new AgentorIntegrationsOptions
        {
            ExternalAgents = new IntegrationFamilyOptions
            {
                Mode = IntegrationAdapterMode.Http,
                Http = new HttpIntegrationOptions { BaseUrl = "http://ext.test/" },
            },
        };

        var sut = new HttpExternalAgentProtocolClient(new StubFactory(httpClient), new StaticMonitor(opts));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            sut.ListCapabilitiesAsync(ExternalAgentProtocolKind.A2AStyled));

        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
        Assert.Contains("403", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_maps_IsNonCanonEvidence_from_wire()
    {
        var body = new ExternalAgentInvocationRequestDto(
            ExternalAgentProtocolKind.A2AStyled,
            "ag",
            "cap",
            new Dictionary<string, string>());

        var responseBody = new ExternalAgentInvocationResultDto(
            ExternalAgentInvocationStatus.Succeeded,
            new Dictionary<string, string> { ["k"] = "v" },
            null,
            IsNonCanonEvidence: false);

        var handler = new LambdaHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(responseBody, options: AgentorHttpJson.Options),
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://ext.test/") };

        var opts = new AgentorIntegrationsOptions
        {
            ExternalAgents = new IntegrationFamilyOptions
            {
                Mode = IntegrationAdapterMode.Http,
                Http = new HttpIntegrationOptions { BaseUrl = "http://ext.test/" },
            },
        };

        var sut = new HttpExternalAgentProtocolClient(new StubFactory(httpClient), new StaticMonitor(opts));

        var result = await sut.InvokeAsync(body, CancellationToken.None);

        Assert.False(result.IsNonCanonEvidence);
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
