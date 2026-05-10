using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Agentor.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Agentor.Api.Tests;

public sealed class AgentorApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly WebApplicationFactory<Program> _factory;

    public AgentorApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostAgentRuns_ReturnsAcceptedRun()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/agent-runs", new StartAgentRunRequestDto(
            "PR1 Agent",
            "Prove API smoke path.",
            "api-test-trace"));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var run = await response.Content.ReadFromJsonAsync<AgentRunDto>(JsonOptions);
        Assert.NotNull(run);
        Assert.Equal("api-test-trace", run!.TraceId);
        Assert.NotEmpty(run.Steps);
    }
}
