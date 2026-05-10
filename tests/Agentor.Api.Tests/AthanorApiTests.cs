using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Agentor.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Agentor.Api.Tests;

public sealed class AthanorApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly WebApplicationFactory<Program> _factory;

    public AthanorApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetLatestSnapshot_UnknownRun_Returns404()
    {
        using var client = _factory.CreateClient();
        var id = Guid.NewGuid();
        var res = await client.GetAsync($"/api/v1/agent-runs/{id}/athanor/latest-snapshot");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task PostEvidenceProvenance_AfterCompletedStartRun_Returns409()
    {
        using var client = _factory.CreateClient();
        var start = await client.PostAsJsonAsync("/api/v1/agent-runs", new StartAgentRunRequestDto(
            "Athanor API test agent",
            "Objective for Athanor API test.",
            null));
        Assert.Equal(HttpStatusCode.Accepted, start.StatusCode);
        var run = await start.Content.ReadFromJsonAsync<AgentRunDto>(JsonOptions);
        Assert.NotNull(run);

        var res = await client.PostAsJsonAsync(
            $"/api/v1/agent-runs/{run!.Id}/athanor/evidence-provenance",
            new AttachEvidenceProvenanceRequestDto("any-query"));
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }
}