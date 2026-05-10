using System.Net;
using System.Net.Http.Json;
using Agentor.Contracts.KnowledgeState;
using Agentor.Infrastructure.Athanor;
using Agentor.Infrastructure.Http;
using Agentor.Infrastructure.Options;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

namespace Agentor.Infrastructure.Tests;

public sealed class HttpKnowledgeStateClientTests
{
    [Fact]
    public async Task GetLatestSnapshotAsync_ReturnsDto_FromStubHttp()
    {
        var projectId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var snapshot = new CanonicalSnapshotDto(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            projectId,
            new DateTimeOffset(2025, 1, 2, 3, 4, 5, TimeSpan.Zero),
            [new CanonicalStateEntryDto("k", "v", 1)]);

        var handler = new StubHandler(req =>
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.Contains("snapshots/latest", req.RequestUri!.PathAndQuery, StringComparison.Ordinal);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(snapshot, options: AgentorHttpJson.Options),
            });
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://athanor.test/") };

        var integrationOpts = new AgentorIntegrationsOptions
        {
            Athanor = new IntegrationFamilyOptions
            {
                Mode = IntegrationAdapterMode.Http,
                Http = new HttpIntegrationOptions { BaseUrl = "http://athanor.test/" },
            },
        };

        var sut = new HttpKnowledgeStateClient(new StubHttpClientFactory(httpClient), new StaticMonitor(integrationOpts));

        var result = await sut.GetLatestSnapshotAsync(projectId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(snapshot.SnapshotId, result!.SnapshotId);
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StaticMonitor(AgentorIntegrationsOptions value) : IOptionsMonitor<AgentorIntegrationsOptions>
    {
        public AgentorIntegrationsOptions CurrentValue => value;

        public AgentorIntegrationsOptions Get(string? name) => value;

        public IDisposable OnChange(Action<AgentorIntegrationsOptions, string?> listener) => new EmptyDisposable();

        private sealed class EmptyDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> sender) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            sender(request);
    }
}
