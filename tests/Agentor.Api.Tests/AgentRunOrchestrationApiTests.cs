using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Agentor.Application;
using Agentor.Contracts;
using Agentor.Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Agentor.Api.Tests;

/// <summary>Phase 24 — public POST /api/v1/agent-runs routes through IAgentRunOrchestrator.</summary>
public sealed class AgentRunOrchestrationApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly WebApplicationFactory<Program> _factory;

    public AgentRunOrchestrationApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostAgentRuns_WithToolKey_ConexusModelComplete_Completes()
    {
        using var client = _factory.CreateClient();
        var body = new StartAgentRunRequestDto(
            "Model agent",
            "Say hello for test.",
            "orch-conexus-trace",
            ToolKey: WellKnownToolKeys.ConexusModelComplete);

        var res = await client.PostAsJsonAsync("/api/v1/agent-runs", body, JsonOptions);
        Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);
        var run = await res.Content.ReadFromJsonAsync<AgentRunDto>(JsonOptions);
        Assert.NotNull(run);
        Assert.Equal(AgentRunStatus.Completed, run!.Status);

        var trace = await client.GetFromJsonAsync<List<TraceEventDto>>(
            $"/api/v1/agent-runs/{run.Id}/trace",
            JsonOptions);
        Assert.NotNull(trace);
        Assert.Contains(trace!, e => e.Message.Contains("Tool call completed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PostAgentRuns_WithToolKey_McpEcho_Completes()
    {
        using var client = _factory.CreateClient();
        var key = McpToolKeys.Format("demo-server", "echo");
        var body = new StartAgentRunRequestDto(
            "Mcp agent",
            "Echo objective fallback.",
            "orch-mcp-trace",
            ToolKey: key,
            Input: new Dictionary<string, JsonElement>
            {
                ["text"] = JsonSerializer.SerializeToElement("ping")
            });

        var res = await client.PostAsJsonAsync("/api/v1/agent-runs", body, JsonOptions);
        Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);
        var run = await res.Content.ReadFromJsonAsync<AgentRunDto>(JsonOptions);
        Assert.NotNull(run);
        Assert.Equal(AgentRunStatus.Completed, run!.Status);
    }

    [Fact]
    public async Task PostAgentRuns_WithToolKey_ExternalAgentInvoke_Completes()
    {
        using var client = _factory.CreateClient();
        var body = new StartAgentRunRequestDto(
            "External agent",
            "Invoke fake capability.",
            "orch-ext-trace",
            ToolKey: ExternalAgentToolKeys.Invoke,
            Input: new Dictionary<string, JsonElement>
            {
                ["agentKey"] = JsonSerializer.SerializeToElement("demo-agent"),
                ["capabilityKey"] = JsonSerializer.SerializeToElement("demo-cap"),
                ["protocolKind"] = JsonSerializer.SerializeToElement("A2AStyled")
            });

        var res = await client.PostAsJsonAsync("/api/v1/agent-runs", body, JsonOptions);
        Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);
        var run = await res.Content.ReadFromJsonAsync<AgentRunDto>(JsonOptions);
        Assert.NotNull(run);
        Assert.Equal(AgentRunStatus.Completed, run!.Status);
    }

    [Fact]
    public async Task PostAgentRuns_WithUnknownPlanId_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();
        var body = new StartAgentRunRequestDto(
            "Plan agent",
            "Missing plan.",
            "orch-plan-missing",
            PlanId: Guid.NewGuid());

        var res = await client.PostAsJsonAsync("/api/v1/agent-runs", body, JsonOptions);
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        var err = await res.Content.ReadFromJsonAsync<ApiErrorDto>(JsonOptions);
        Assert.NotNull(err);
        Assert.Equal("PlanNotFound", err!.Error);
    }

    [Fact]
    public async Task PostAgentRuns_WithUnknownRecipeId_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();
        var body = new StartAgentRunRequestDto(
            "Recipe agent",
            "Missing recipe.",
            "orch-recipe-missing",
            RecipeId: Guid.NewGuid());

        var res = await client.PostAsJsonAsync("/api/v1/agent-runs", body, JsonOptions);
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        var err = await res.Content.ReadFromJsonAsync<ApiErrorDto>(JsonOptions);
        Assert.NotNull(err);
        Assert.Equal("RecipeNotFound", err!.Error);
    }

    [Fact]
    public async Task PostAgentRuns_WithUnknownSkillKey_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();
        var body = new StartAgentRunRequestDto(
            "Skill agent",
            "Missing skill.",
            "orch-skill-missing",
            SkillKey: "definitely-not-a-registered-skill");

        var res = await client.PostAsJsonAsync("/api/v1/agent-runs", body, JsonOptions);
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        var err = await res.Content.ReadFromJsonAsync<ApiErrorDto>(JsonOptions);
        Assert.NotNull(err);
        Assert.Equal("SkillNotRegistered", err!.Error);
    }

    [Fact]
    public async Task PostAgentRuns_PolicyDeny_BlocksBeforeToolExecution()
    {
        await using var factory = new DenyConexusToolWebApplicationFactory();
        using var client = factory.CreateClient();
        var body = new StartAgentRunRequestDto(
            "Denied",
            "Should not complete model tool.",
            "orch-deny-trace",
            ToolKey: WellKnownToolKeys.ConexusModelComplete);

        var res = await client.PostAsJsonAsync("/api/v1/agent-runs", body, JsonOptions);
        Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);
        var run = await res.Content.ReadFromJsonAsync<AgentRunDto>(JsonOptions);
        Assert.NotNull(run);
        Assert.Equal(AgentRunStatus.Failed, run!.Status);
    }

    [Fact]
    public async Task PostAgentRuns_PolicyRequiresReview_SingleToolPr1Fake_SuspendsRun()
    {
        await using var factory = new RequiresReviewPr1FakeWebApplicationFactory();
        using var client = factory.CreateClient();
        var body = new StartAgentRunRequestDto(
            "Review agent",
            "Needs review.",
            "orch-review-trace",
            ToolKey: WellKnownToolKeys.Pr1FakeTool);

        var res = await client.PostAsJsonAsync("/api/v1/agent-runs", body, JsonOptions);
        Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);
        var run = await res.Content.ReadFromJsonAsync<AgentRunDto>(JsonOptions);
        Assert.NotNull(run);
        Assert.Equal(AgentRunStatus.RequiresReview, run!.Status);
    }

    [Fact]
    public async Task PostAgentRuns_ModeLegacyExplicit_WorksWhenImplicitDisabled()
    {
        await using var factory = new NoImplicitLegacyWebApplicationFactory();
        using var client = factory.CreateClient();
        var body = new StartAgentRunRequestDto(
            "Legacy",
            "Explicit legacy.",
            "orch-legacy-trace",
            Mode: RunExecutionMode.LegacyFakeTool);

        var res = await client.PostAsJsonAsync("/api/v1/agent-runs", body, JsonOptions);
        Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);
        var run = await res.Content.ReadFromJsonAsync<AgentRunDto>(JsonOptions);
        Assert.NotNull(run);
        Assert.Equal(AgentRunStatus.Completed, run!.Status);
    }
}

internal sealed class DenyConexusToolWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Agentor:RuntimePolicy:ActiveProfile:DeniedToolKeys:0"] = WellKnownToolKeys.ConexusModelComplete
            });
        });
    }
}

internal sealed class RequiresReviewPr1FakeWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Agentor:RuntimePolicy:ActiveProfile:RequiresReviewToolKeys:0"] = WellKnownToolKeys.Pr1FakeTool
            });
        });
    }
}

internal sealed class NoImplicitLegacyWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Agentor:PublicRuns:TreatMissingExecutionSelectorAsLegacyFakeTool"] = "false"
            });
        });
    }
}
