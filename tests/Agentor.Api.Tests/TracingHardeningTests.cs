using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Agentor.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Agentor.Api.Tests;

public sealed class TracingHardeningTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string TraceIdHeader = "X-Agentor-Trace-Id";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly WebApplicationFactory<Program> _factory;

    public TracingHardeningTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostAgentRuns_Success_ResponseHasTraceIdHeader()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/agent-runs", new StartAgentRunRequestDto(
            "Trace Agent",
            "Verify trace header on success.",
            null));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.True(response.Headers.Contains(TraceIdHeader), "Success response must include X-Agentor-Trace-Id header.");
        Assert.False(string.IsNullOrWhiteSpace(response.Headers.GetValues(TraceIdHeader).FirstOrDefault()));
    }

    [Fact]
    public async Task PostAgentRuns_ValidationError_ResponseHasTraceIdHeader()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/agent-runs", new StartAgentRunRequestDto(
            "Agent",
            "",
            null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(response.Headers.Contains(TraceIdHeader), "Error response must include X-Agentor-Trace-Id header.");
    }

    [Fact]
    public async Task GetAgentRun_NotFound_ResponseHasTraceIdHeader()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/agent-runs/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.True(response.Headers.Contains(TraceIdHeader), "Not-found response must include X-Agentor-Trace-Id header.");
    }

    [Fact]
    public async Task PostAgentRuns_ValidationError_ErrorDtoContainsTraceId()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/agent-runs", new StartAgentRunRequestDto(
            "Agent",
            "",
            null));

        var error = await response.Content.ReadFromJsonAsync<ApiErrorDto>(JsonOptions);
        Assert.NotNull(error);
        Assert.False(string.IsNullOrWhiteSpace(error!.TraceId), "Error DTO must include a TraceId value.");
    }

    [Fact]
    public async Task PostAgentRuns_RequestWithExplicitTraceId_ResponseEchoesTraceId()
    {
        using var client = _factory.CreateClient();
        const string explicitTraceId = "explicit-trace-abc";

        var response = await client.PostAsJsonAsync("/api/v1/agent-runs", new StartAgentRunRequestDto(
            "Trace Agent",
            "Verify trace echo.",
            explicitTraceId));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var run = await response.Content.ReadFromJsonAsync<AgentRunDto>(JsonOptions);
        Assert.NotNull(run);
        Assert.Equal(explicitTraceId, run!.TraceId);
    }

    [Fact]
    public async Task PostAgentRuns_WithEmptyObjective_ErrorDtoHasErrorsList()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/agent-runs", new StartAgentRunRequestDto(
            "Agent",
            "",
            null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ApiErrorDto>(JsonOptions);
        Assert.NotNull(error);
        Assert.Equal("ValidationError", error!.Error);
        Assert.NotNull(error.Errors);
        Assert.NotEmpty(error.Errors!);
    }

    [Fact]
    public async Task PostAgentRuns_WithOverlongObjective_ReturnsBadRequestWithErrorsList()
    {
        using var client = _factory.CreateClient();
        var tooLong = new string('x', 2001);

        var response = await client.PostAsJsonAsync("/api/v1/agent-runs", new StartAgentRunRequestDto(
            "Agent",
            tooLong,
            null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ApiErrorDto>(JsonOptions);
        Assert.NotNull(error);
        Assert.NotNull(error!.Errors);
        Assert.NotEmpty(error.Errors!);
    }
}
