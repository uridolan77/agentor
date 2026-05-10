using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Agentor.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Agentor.Api.Tests;

public sealed class ApiIntegrationBaselineTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task PostThenGet_RoundTripsThroughHttpStack()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var post = await client.PostAsJsonAsync(
            "/api/v1/agent-runs",
            new StartAgentRunRequestDto("Integration Agent", "Integration baseline.", "int-baseline-trace"));
        Assert.Equal(HttpStatusCode.Accepted, post.StatusCode);
        var created = await post.Content.ReadFromJsonAsync<AgentRunDto>(JsonOptions);
        Assert.NotNull(created);

        var get = await client.GetAsync($"/api/v1/agent-runs/{created!.Id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var loaded = await get.Content.ReadFromJsonAsync<AgentRunDto>(JsonOptions);
        Assert.NotNull(loaded);
        Assert.Equal(created.Id, loaded!.Id);
        Assert.Equal("int-baseline-trace", loaded.TraceId);
    }
}