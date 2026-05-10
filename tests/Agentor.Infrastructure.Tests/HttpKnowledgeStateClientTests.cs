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

    [Fact]
    public async Task LookupCanonicalEntryAsync_ReturnsNull_On404()
    {
        var projectId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var handler = new StubHandler(req =>
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.Contains("/canonical/", req.RequestUri!.PathAndQuery, StringComparison.Ordinal);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
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

        var result = await sut.LookupCanonicalEntryAsync(projectId, "my-key", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetLatestSnapshotAsync_ReturnsNull_On404()
    {
        var projectId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var handler = new StubHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));

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

        Assert.Null(result);
    }

    [Fact]
    public async Task SearchEvidenceAsync_ReturnsHits_On200()
    {
        var projectId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var hits = new List<EvidenceSearchResultDto> { new(Guid.NewGuid(), "t", "s") };

        var handler = new StubHandler(req =>
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.Contains("evidence/search", req.RequestUri!.PathAndQuery, StringComparison.Ordinal);
            Assert.Contains("query=", req.RequestUri.Query, StringComparison.Ordinal);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(hits, options: AgentorHttpJson.Options),
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

        var result = await sut.SearchEvidenceAsync(projectId, "q space", CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("t", result[0].Title);
    }

    [Fact]
    public async Task SearchEvidenceAsync_ReturnsEmptyList_WhenBodyNull()
    {
        var projectId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var handler = new StubHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("null") }));

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

        var result = await sut.SearchEvidenceAsync(projectId, "q", CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task SubmitCandidateAsync_On5xx_throws_HttpRequestException_with_status()
    {
        var projectId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var runId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var submission = new CandidateKnowledgeSubmissionDto("s", "{}");

        var handler = new StubHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway)
            {
                Content = new StringContent("upstream"),
            }));

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

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            sut.SubmitCandidateAsync(projectId, runId, submission, CancellationToken.None));

        Assert.Contains("502", ex.Message, StringComparison.Ordinal);
        Assert.Contains("upstream", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueueForReviewAsync_ReturnsDto_On200()
    {
        var projectId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var candidateId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var actorId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var dto = new ReviewQueueResultDto(Guid.NewGuid(), "queued");

        var handler = new StubHandler(req =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Contains("/review-queue", req.RequestUri!.PathAndQuery, StringComparison.Ordinal);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(dto, options: AgentorHttpJson.Options),
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

        var result = await sut.QueueForReviewAsync(projectId, candidateId, actorId, CancellationToken.None);

        Assert.Equal("queued", result.Status);
    }

    [Fact]
    public void Athanor_http_relative_paths_exclude_Canonize_segment()
    {
        var projectId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var runId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var candidateId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var paths = new[]
        {
            $"v1/projects/{projectId}/snapshots/latest",
            $"v1/projects/{projectId}/canonical/key",
            $"v1/projects/{projectId}/evidence/search?query=q",
            $"v1/projects/{projectId}/runs/{runId}/candidates",
            $"v1/projects/{projectId}/candidates/{candidateId}/review-queue",
        };

        foreach (var p in paths)
        {
            Assert.DoesNotContain("canonize", p, StringComparison.OrdinalIgnoreCase);
        }
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
