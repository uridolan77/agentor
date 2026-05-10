using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Agentor.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Agentor.Api.Tests;

public sealed class ApiContractTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly WebApplicationFactory<Program> _factory;

    public ApiContractTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostAgentRuns_WithEmptyObjective_ReturnsBadRequestWithErrorDto()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/agent-runs", new StartAgentRunRequestDto(
            "Test Agent",
            "",
            null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ApiErrorDto>(JsonOptions);
        Assert.NotNull(error);
        Assert.Equal("ValidationError", error!.Error);
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
    }

    [Fact]
    public async Task PostAgentRuns_WithNullAgentName_UsesDefaultAndReturnsAccepted()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/agent-runs", new StartAgentRunRequestDto(
            "",
            "Verify agent name default fallback.",
            null));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var run = await response.Content.ReadFromJsonAsync<AgentRunDto>(JsonOptions);
        Assert.NotNull(run);
        Assert.False(string.IsNullOrWhiteSpace(run!.AgentName));
    }

    [Fact]
    public async Task GetAgentRun_WithUnknownId_ReturnsNotFoundWithErrorDto()
    {
        using var client = _factory.CreateClient();
        var unknownId = Guid.NewGuid();

        var response = await client.GetAsync($"/api/v1/agent-runs/{unknownId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ApiErrorDto>(JsonOptions);
        Assert.NotNull(error);
        Assert.Equal("RunNotFound", error!.Error);
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
    }

    [Fact]
    public async Task GetRunManifest_AfterCreatingRun_ReturnsManifest()
    {
        using var client = _factory.CreateClient();

        var postResponse = await client.PostAsJsonAsync("/api/v1/agent-runs", new StartAgentRunRequestDto(
            "Manifest Agent",
            "Verify manifest round-trip.",
            "manifest-test-trace"));

        Assert.Equal(HttpStatusCode.Accepted, postResponse.StatusCode);

        var run = await postResponse.Content.ReadFromJsonAsync<AgentRunDto>(JsonOptions);
        Assert.NotNull(run);

        var manifestResponse = await client.GetAsync($"/api/v1/agent-runs/{run!.Id}/manifest");

        Assert.Equal(HttpStatusCode.OK, manifestResponse.StatusCode);

        var manifest = await manifestResponse.Content.ReadFromJsonAsync<RunManifestDto>(JsonOptions);
        Assert.NotNull(manifest);
        Assert.Equal(run.Id, manifest!.RunId);
        Assert.Equal("manifest-test-trace", manifest.TraceId);
        Assert.True(manifest.StepCount >= 1);
        Assert.True(manifest.ToolCallCount >= 1);
        Assert.True(manifest.PolicyDecisionCount >= 1);
        Assert.True(manifest.TraceEventCount >= 1);
    }

    [Fact]
    public async Task GetRunManifest_WithUnknownId_ReturnsNotFoundWithErrorDto()
    {
        using var client = _factory.CreateClient();
        var unknownId = Guid.NewGuid();

        var response = await client.GetAsync($"/api/v1/agent-runs/{unknownId}/manifest");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ApiErrorDto>(JsonOptions);
        Assert.NotNull(error);
        Assert.Equal("RunNotFound", error!.Error);
    }

    [Fact]
    public async Task GetOpenApiDocument_ReturnsOk()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Content.Headers.ContentType);

        var body = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(body));

        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.TryGetProperty("info", out _), "OpenAPI document should have an 'info' property.");
        Assert.True(doc.RootElement.TryGetProperty("paths", out _), "OpenAPI document should have a 'paths' property.");
    }
}
