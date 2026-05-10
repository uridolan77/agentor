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
        Assert.NotNull(error.Errors);
        Assert.NotEmpty(error.Errors!);
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
        Assert.Equal("1.0", manifest.ManifestVersion);
        Assert.Matches("^[0-9a-f]{64}$", manifest.ContentHash);
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

/// <summary>
/// Isolated factory per test to avoid cross-test pollution in the singleton in-memory repository.
/// </summary>
public sealed class AgentRunQueryEndpointsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task ListAgentRuns_EmptyRepository_ReturnsEmptyItems()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/agent-runs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AgentRunListResponseDto>(JsonOptions);
        Assert.NotNull(body);
        Assert.Empty(body!.Items);
        Assert.Equal(0, body.TotalCount);
        Assert.Equal(0, body.Skip);
        Assert.Equal(20, body.Take);
    }

    [Fact]
    public async Task ListAgentRuns_AfterCreatingRun_ReturnsSummaryRow()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var postResponse = await client.PostAsJsonAsync("/api/v1/agent-runs", new StartAgentRunRequestDto(
            "List Agent",
            "Verify list endpoint.",
            "list-query-trace"));

        Assert.Equal(HttpStatusCode.Accepted, postResponse.StatusCode);
        var created = await postResponse.Content.ReadFromJsonAsync<AgentRunDto>(JsonOptions);
        Assert.NotNull(created);

        var listResponse = await client.GetAsync("/api/v1/agent-runs?skip=0&take=10");

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = await listResponse.Content.ReadFromJsonAsync<AgentRunListResponseDto>(JsonOptions);
        Assert.NotNull(list);
        Assert.Equal(1, list!.TotalCount);
        Assert.Single(list.Items);
        Assert.Equal(created!.Id, list.Items[0].Id);
        Assert.Equal("list-query-trace", list.Items[0].TraceId);
    }

    [Fact]
    public async Task GetAgentRunTrace_UnknownRun_ReturnsNotFound()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/agent-runs/{Guid.NewGuid()}/trace");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorDto>(JsonOptions);
        Assert.Equal("RunNotFound", error!.Error);
    }

    [Fact]
    public async Task GetAgentRunTrace_AfterCreatingRun_ReturnsTraceEvents()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var postResponse = await client.PostAsJsonAsync("/api/v1/agent-runs", new StartAgentRunRequestDto(
            "Trace Agent",
            "Verify trace endpoint.",
            "trace-endpoint-trace"));

        var run = await postResponse.Content.ReadFromJsonAsync<AgentRunDto>(JsonOptions);
        Assert.NotNull(run);

        var response = await client.GetAsync($"/api/v1/agent-runs/{run!.Id}/trace");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var trace = await response.Content.ReadFromJsonAsync<List<TraceEventDto>>(JsonOptions);
        Assert.NotNull(trace);
        Assert.NotEmpty(trace!);
        Assert.Equal(run.Trace.Count, trace.Count);
    }

    [Fact]
    public async Task GetAgentRunSteps_UnknownRun_ReturnsNotFound()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/agent-runs/{Guid.NewGuid()}/steps");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAgentRunSteps_AfterCreatingRun_ReturnsSteps()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var postResponse = await client.PostAsJsonAsync("/api/v1/agent-runs", new StartAgentRunRequestDto(
            "Steps Agent",
            "Verify steps endpoint.",
            "steps-endpoint-trace"));

        var run = await postResponse.Content.ReadFromJsonAsync<AgentRunDto>(JsonOptions);
        Assert.NotNull(run);

        var response = await client.GetAsync($"/api/v1/agent-runs/{run!.Id}/steps");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var steps = await response.Content.ReadFromJsonAsync<List<AgentStepDto>>(JsonOptions);
        Assert.NotNull(steps);
        Assert.NotEmpty(steps!);
        Assert.Equal(run.Steps.Count, steps.Count);
    }

    [Fact]
    public async Task GetAgentRunToolCalls_UnknownRun_ReturnsNotFound()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/agent-runs/{Guid.NewGuid()}/tool-calls");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAgentRunToolCalls_AfterCreatingRun_ReturnsToolCalls()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var postResponse = await client.PostAsJsonAsync("/api/v1/agent-runs", new StartAgentRunRequestDto(
            "ToolCalls Agent",
            "Verify tool-calls endpoint.",
            "toolcalls-endpoint-trace"));

        var run = await postResponse.Content.ReadFromJsonAsync<AgentRunDto>(JsonOptions);
        Assert.NotNull(run);

        var response = await client.GetAsync($"/api/v1/agent-runs/{run!.Id}/tool-calls");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var toolCalls = await response.Content.ReadFromJsonAsync<List<ToolCallDto>>(JsonOptions);
        Assert.NotNull(toolCalls);
        Assert.NotEmpty(toolCalls!);
        var expectedCount = run.Steps.Sum(s => s.ToolCalls.Count);
        Assert.Equal(expectedCount, toolCalls.Count);
    }

    [Fact]
    public async Task ListAgentRuns_MultipleRuns_ReturnsNewestFirst()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var first = await client.PostAsJsonAsync("/api/v1/agent-runs", new StartAgentRunRequestDto(
            "A", "First run.", "order-trace-a"));
        var runA = await first.Content.ReadFromJsonAsync<AgentRunDto>(JsonOptions);
        Assert.NotNull(runA);

        await Task.Delay(25);

        var second = await client.PostAsJsonAsync("/api/v1/agent-runs", new StartAgentRunRequestDto(
            "B", "Second run.", "order-trace-b"));
        var runB = await second.Content.ReadFromJsonAsync<AgentRunDto>(JsonOptions);
        Assert.NotNull(runB);

        var listResponse = await client.GetAsync("/api/v1/agent-runs?skip=0&take=10");
        var list = await listResponse.Content.ReadFromJsonAsync<AgentRunListResponseDto>(JsonOptions);

        Assert.NotNull(list);
        Assert.Equal(2, list!.TotalCount);
        Assert.Equal(runB!.Id, list.Items[0].Id);
        Assert.Equal(runA!.Id, list.Items[1].Id);
    }
}
