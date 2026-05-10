using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Agentor.Application;
using Agentor.Contracts;
using Agentor.Domain.Enums;
using Agentor.Domain.Governance;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Agentor.Api.Tests;

public sealed class Phase13ProductSurfaceApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly WebApplicationFactory<Program> _factory;

    public Phase13ProductSurfaceApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetOperatorDashboard_ReturnsModulesWithLinks()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/operator/dashboard");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<OperatorDashboardResponseDto>(JsonOptions);
        Assert.NotNull(dto);
        Assert.True(dto!.Modules.ContainsKey("runs"));
        Assert.True(dto.Modules.ContainsKey("reviews"));
        Assert.True(dto.Modules.ContainsKey("queue"));
        Assert.True(dto.Modules.ContainsKey("outbox"));
        Assert.True(dto.Modules.ContainsKey("integrations"));
        Assert.True(dto.Modules.ContainsKey("deferredRisks"));
        Assert.True(dto.Modules.ContainsKey("policyRuntime"));
    }

    [Fact]
    public async Task PostRecipes_InvalidRecipe_ReturnsBadRequest()
    {
        using var client = _factory.CreateClient();
        var body = new CreateRecipeRequestDto(
            "",
            "1.0.0",
            CoordinationTopology.SequentialPipeline,
            [],
            FailureHandlingPolicy.FailFast,
            null);

        var response = await client.PostAsJsonAsync("/api/v1/recipes", body, JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostRecipes_ValidToolRecipe_ThenGetAndPlanRoundTrip()
    {
        using var client = _factory.CreateClient();
        var recipeBody = new CreateRecipeRequestDto(
            "phase13-test-recipe",
            "1.0.0",
            CoordinationTopology.SequentialPipeline,
            [
                new RecipeStepRequestDto(
                    "s1",
                    0,
                    RecipeStepKind.Tool,
                    WellKnownToolKeys.Pr1FakeTool)
            ],
            FailureHandlingPolicy.FailFast,
            null);

        var createRecipe = await client.PostAsJsonAsync("/api/v1/recipes", recipeBody, JsonOptions);
        Assert.Equal(HttpStatusCode.Created, createRecipe.StatusCode);
        var recipe = await createRecipe.Content.ReadFromJsonAsync<RecipeArtifactResponseDto>(JsonOptions);
        Assert.NotNull(recipe);

        var getRecipe = await client.GetAsync($"/api/v1/recipes/{recipe!.Id}");
        Assert.Equal(HttpStatusCode.OK, getRecipe.StatusCode);

        var planBody = new CreatePlanFromRecipeRequestDto(recipe.Id, null);
        var createPlan = await client.PostAsJsonAsync("/api/v1/plans", planBody, JsonOptions);
        Assert.Equal(HttpStatusCode.Created, createPlan.StatusCode);
        var plan = await createPlan.Content.ReadFromJsonAsync<PlanArtifactResponseDto>(JsonOptions);
        Assert.NotNull(plan);
        Assert.Equal(recipe.Id, plan!.RecipeId);
    }

    [Fact]
    public async Task GetRunTimeline_UnknownRun_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();
        var id = Guid.NewGuid();
        var response = await client.GetAsync($"/api/v1/runs/{id}/timeline");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetRunAuditPacket_MatchesAuditExportHeader_ForSameRun()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Agentor-Actor-Id", Guid.NewGuid().ToString("D"));

        var start = await client.PostAsJsonAsync("/api/v1/agent-runs", new StartAgentRunRequestDto(
            "Phase13",
            "audit alias",
            null));

        Assert.Equal(HttpStatusCode.Accepted, start.StatusCode);
        var run = await start.Content.ReadFromJsonAsync<AgentRunDto>(JsonOptions);
        Assert.NotNull(run);

        var export = await client.GetAsync($"/api/v1/agent-runs/{run!.Id}/audit-export");
        var packet = await client.GetAsync($"/api/v1/runs/{run.Id}/audit-packet");

        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        Assert.Equal(HttpStatusCode.OK, packet.StatusCode);

        Assert.True(export.Headers.TryGetValues("X-Agentor-Audit-Content-SHA256", out var eHash));
        Assert.True(packet.Headers.TryGetValues("X-Agentor-Audit-Content-SHA256", out var pHash));
        Assert.Equal(eHash!.Single(), pHash!.Single());
    }

    [Fact]
    public async Task PostSkillPackage_Duplicate_ReturnsConflict()
    {
        using var client = _factory.CreateClient();
        var body = new CreateSkillPackageRequestDto(
            "phase13.skill",
            "1.0.0",
            "n",
            "p",
            [new SkillProcedureStepRequestDto("p1", 0, "seg", SkillProcedureStepKind.Segment)]);

        var first = await client.PostAsJsonAsync("/api/v1/skills", body, JsonOptions);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/v1/skills", body, JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task GetReviewsPending_ReturnsOk_WithItemsArray()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/reviews/pending?skip=0&take=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<PendingHumanReviewListResponseDto>(JsonOptions);
        Assert.NotNull(dto);
        Assert.NotNull(dto!.Items);
        Assert.True(dto.TotalCount >= 0);
        Assert.Equal(0, dto.Skip);
        Assert.Equal(10, dto.Take);
    }

    [Fact]
    public async Task PostReviewDecision_CompletedRun_ReturnsConflict()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Agentor-Actor-Id", Guid.NewGuid().ToString("D"));

        var start = await client.PostAsJsonAsync("/api/v1/agent-runs", new StartAgentRunRequestDto(
            "Phase13",
            "review alias conflict",
            null));

        Assert.Equal(HttpStatusCode.Accepted, start.StatusCode);
        var run = await start.Content.ReadFromJsonAsync<AgentRunDto>(JsonOptions);
        Assert.NotNull(run);
        Assert.Equal(AgentRunStatus.Completed, run!.Status);

        var decision = await client.PostAsJsonAsync(
            $"/api/v1/reviews/{run.Id}/decisions",
            new ApplyHumanReviewRequestDto(ReviewDecisionKind.Approve),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, decision.StatusCode);
    }
}