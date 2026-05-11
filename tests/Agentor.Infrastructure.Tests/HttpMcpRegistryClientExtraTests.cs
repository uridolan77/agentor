using System.Net;
using System.Net.Http.Json;
using Agentor.Domain;
using Agentor.Domain.Enums;
using Agentor.Infrastructure.Http;
using Agentor.Infrastructure.Mcp;
using Agentor.Infrastructure.Options;
using Microsoft.Extensions.Options;
using Xunit;

namespace Agentor.Infrastructure.Tests;

public sealed class HttpMcpRegistryClientExtraTests
{
    [Fact]
    public async Task ListServersAsync_GetsV1Servers()
    {
        var wire = new[] { new { Id = "s1", DisplayName = "Server One" } };

        var handler = new LambdaHandler((req, _) =>
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.EndsWith("/v1/servers", req.RequestUri!.AbsolutePath, StringComparison.Ordinal);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(wire, options: AgentorHttpJson.Options),
            });
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://mcp.test/") };

        var opts = new AgentorIntegrationsOptions
        {
            Mcp = new IntegrationFamilyOptions
            {
                Mode = IntegrationAdapterMode.Http,
                Http = new HttpIntegrationOptions { BaseUrl = "http://mcp.test/" },
            },
        };

        var sut = new HttpMcpRegistryClient(new StubFactory(httpClient), new StaticMonitor(opts));

        var list = await sut.ListServersAsync();

        Assert.Single(list);
        Assert.Equal("s1", list[0].Id);
    }

    [Fact]
    public async Task InvokeToolAsync_PostsInvokePath()
    {
        var handler = new LambdaHandler((req, _) =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Contains("/v1/servers/s1/tools/t1/invoke", req.RequestUri!.AbsolutePath, StringComparison.Ordinal);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(
                    new { Success = true, Output = new Dictionary<string, string> { ["o"] = "v" }, ErrorMessage = (string?)null },
                    options: AgentorHttpJson.Options),
            });
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://mcp.test/") };

        var opts = new AgentorIntegrationsOptions
        {
            Mcp = new IntegrationFamilyOptions
            {
                Mode = IntegrationAdapterMode.Http,
                Http = new HttpIntegrationOptions { BaseUrl = "http://mcp.test/" },
            },
        };

        var sut = new HttpMcpRegistryClient(new StubFactory(httpClient), new StaticMonitor(opts));

        var result = await sut.InvokeToolAsync("s1", "t1", ToolPayload.FromLegacyDictionary(new Dictionary<string, string>()), CancellationToken.None);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task ListToolsAsync_GetsV1ServersTools()
    {
        var wire = new[]
        {
            new { ServerId = "s1", Name = "echo", Description = "d", NominalRisk = "low" },
        };

        var handler = new LambdaHandler((req, _) =>
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.EndsWith("/v1/servers/s1/tools", req.RequestUri!.AbsolutePath, StringComparison.Ordinal);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(wire, options: AgentorHttpJson.Options),
            });
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://mcp.test/") };

        var opts = new AgentorIntegrationsOptions
        {
            Mcp = new IntegrationFamilyOptions
            {
                Mode = IntegrationAdapterMode.Http,
                Http = new HttpIntegrationOptions { BaseUrl = "http://mcp.test/" },
            },
        };

        var sut = new HttpMcpRegistryClient(new StubFactory(httpClient), new StaticMonitor(opts));

        var list = await sut.ListToolsAsync("s1");

        Assert.Single(list);
        Assert.Equal("echo", list[0].Name);
    }

    [Fact]
    public async Task ListToolsAsync_invalid_nominalRisk_defaults_to_medium()
    {
        var wire = new[]
        {
            new { ServerId = "s1", Name = "t", Description = "d", NominalRisk = "not-a-real-level" },
        };

        var handler = new LambdaHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(wire, options: AgentorHttpJson.Options),
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://mcp.test/") };

        var opts = new AgentorIntegrationsOptions
        {
            Mcp = new IntegrationFamilyOptions
            {
                Mode = IntegrationAdapterMode.Http,
                Http = new HttpIntegrationOptions { BaseUrl = "http://mcp.test/" },
            },
        };

        var sut = new HttpMcpRegistryClient(new StubFactory(httpClient), new StaticMonitor(opts));

        var list = await sut.ListToolsAsync("s1");

        Assert.Single(list);
        Assert.Equal(ToolRiskLevel.Medium, list[0].NominalRisk);
    }

    [Fact]
    public async Task ListServersAsync_on5xx_throws_HttpRequestException()
    {
        var handler = new LambdaHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("boom"),
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://mcp.test/") };

        var opts = new AgentorIntegrationsOptions
        {
            Mcp = new IntegrationFamilyOptions
            {
                Mode = IntegrationAdapterMode.Http,
                Http = new HttpIntegrationOptions { BaseUrl = "http://mcp.test/" },
            },
        };

        var sut = new HttpMcpRegistryClient(new StubFactory(httpClient), new StaticMonitor(opts));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => sut.ListServersAsync());

        Assert.Equal(HttpStatusCode.InternalServerError, ex.StatusCode);
        Assert.Contains("500", ex.Message, StringComparison.Ordinal);
        Assert.Contains("boom", ex.Message, StringComparison.Ordinal);
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
