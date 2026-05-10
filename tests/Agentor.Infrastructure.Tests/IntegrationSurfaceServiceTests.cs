using System.Net;
using Agentor.Infrastructure.Athanor;
using Agentor.Infrastructure.Conexus;
using Agentor.Infrastructure.HttpResilience;
using Agentor.Infrastructure.IntegrationStatus;
using Agentor.Infrastructure.Mcp;
using Agentor.Infrastructure.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Agentor.Infrastructure.Tests;

public sealed class IntegrationSurfaceServiceTests
{
    [Fact]
    public async Task GetStatusAsync_HttpProbe_ReturnsNotReady_On500()
    {
        var integrations = DefaultIntegrations();
        integrations.Athanor = new IntegrationFamilyOptions
        {
            Mode = IntegrationAdapterMode.Http,
            Http = new HttpIntegrationOptions { BaseUrl = "http://athanor.test/" },
        };

        using var httpClient = new HttpClient(new FixedStatusHandler(HttpStatusCode.InternalServerError))
        {
            BaseAddress = new Uri("http://athanor.test/"),
        };

        var sut = CreateSut(integrations, httpClient);

        var status = await sut.GetStatusAsync();

        Assert.False(status.Ready);
        Assert.False(status.Integrations["athanor"].Ready);
        Assert.Equal("http_500", status.Integrations["athanor"].Detail);
    }

    [Fact]
    public async Task GetStatusAsync_DisabledFamily_HasDisabledDetail()
    {
        var integrations = DefaultIntegrations();
        integrations.Conexus = new IntegrationFamilyOptions { Mode = IntegrationAdapterMode.Disabled };

        using var httpClient = new HttpClient(new FixedStatusHandler(HttpStatusCode.OK))
        {
            BaseAddress = new Uri("http://unused.local/"),
        };

        var sut = CreateSut(integrations, httpClient);

        var status = await sut.GetStatusAsync();

        Assert.True(status.Integrations["conexus"].Ready);
        Assert.Equal("disabled", status.Integrations["conexus"].Detail);
    }

    private static AgentorIntegrationsOptions DefaultIntegrations() =>
        new()
        {
            Athanor = new IntegrationFamilyOptions { Mode = IntegrationAdapterMode.Fake },
            Conexus = new IntegrationFamilyOptions { Mode = IntegrationAdapterMode.Fake },
            Mcp = new IntegrationFamilyOptions { Mode = IntegrationAdapterMode.Fake },
            ExternalAgents = new IntegrationFamilyOptions { Mode = IntegrationAdapterMode.Fake },
        };

    private static IntegrationSurfaceService CreateSut(
        AgentorIntegrationsOptions integrations,
        HttpClient httpClient)
    {
        var factory = new StubHttpClientFactory(httpClient);
        var integrationMonitor = new StaticMonitor<AgentorIntegrationsOptions>(integrations);
        var persistenceMonitor = new StaticMonitor<AgentorPersistenceOptions>(new AgentorPersistenceOptions());

        return new IntegrationSurfaceService(
            integrationMonitor,
            persistenceMonitor,
            factory,
            new ThrowingScopeFactory(),
            new TransportResilienceRegistry());
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StaticMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable OnChange(Action<T, string?> listener) => new EmptyDisposable();

        private sealed class EmptyDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    private sealed class ThrowingScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() =>
            throw new InvalidOperationException("Unexpected scope (persistence should be InMemory for this test).");

        public AsyncServiceScope CreateAsyncScope() =>
            throw new InvalidOperationException("Unexpected scope (persistence should be InMemory for this test).");
    }

    private sealed class FixedStatusHandler(HttpStatusCode code) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(code));
    }
}
