using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Agentor.Api.Tests.Support;
using Agentor.Application;
using Agentor.Application.Abstractions;
using Agentor.Contracts;
using Agentor.Domain;
using Agentor.Domain.Enums;
using Agentor.Domain.Governance;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Agentor.Api.Tests;

/// <summary>
/// PR89 — API integration tests for multi-step human review resume semantics.
/// Covers all four ReviewDecisionKind paths via the governance endpoint.
/// </summary>
public sealed class GovernanceResumeApiTests : IClassFixture<GovernanceResumeApiFixture>
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly GovernanceResumeApiFixture _factory;

    public GovernanceResumeApiTests(GovernanceResumeApiFixture factory)
    {
        _factory = factory;
    }

    private static AgentRun BuildRunInRequiresReview(DateTimeOffset now)
    {
        var run = AgentRun.Start(Guid.NewGuid(), "GovernanceTestAgent", "Multi-step test objective.", "trace-gov-api", now);
        var step = run.StartStep("Step-Blocked", now);

        var toolCall = ToolCall.Start(run.Id, step.Id, WellKnownToolKeys.Pr1FakeTool, new Dictionary<string, string>(), now);
        step.AddToolCall(toolCall);
        toolCall.MarkRequiresReview("Policy required review", now);
        step.MarkRequiresReview(now);
        run.EnterRequiresReview("Policy required review", now);

        // Cursor: s3 is the remaining step after the blocked s2
        var cursor = new PlanResumeCursor(
            PlanId: Guid.NewGuid(),
            BlockedAtPlanStepId: Guid.NewGuid(),
            BlockedAtSourceStepId: "s2",
            BlockedAtToolKey: WellKnownToolKeys.Pr1FakeTool,
            RemainingSteps: new List<PendingPlanStep>
            {
                new PendingPlanStep(
                    Guid.NewGuid(), "s3", 3,
                    WellKnownToolKeys.Pr1FakeTool,
                    RecipeStepKind.Tool,
                    FailureHandlingPolicy.FailFast,
                    null, null)
            },
            CompletedStepHistory: new List<PlanStepResumeSnapshot>
            {
                new PlanStepResumeSnapshot(Guid.NewGuid(), "s1", AgentPlanStepStatus.Completed, true,
                    new Dictionary<string, string> { ["result"] = "s1-done" })
            },
            SuspendedAt: now);

        run.RecordPlanResumeCursor(cursor, now);
        return run;
    }

    private HttpContent ReviewBody(ReviewDecisionKind kind, string? note = null) =>
        new StringContent(
            JsonSerializer.Serialize(new ApplyHumanReviewRequestDto(kind, note), JsonOpts),
            System.Text.Encoding.UTF8,
            "application/json");

    [Fact]
    public async Task Approve_MultiStepRun_CompletesRunAndReturns200()
    {
        var run = BuildRunInRequiresReview(DateTimeOffset.UtcNow);
        _factory.Repository.Seed(run);

        using var client = _factory.CreateClient();
        var response = await client.PostAsync(
            $"/api/v1/agent-runs/{run.Id}/human-review",
            ReviewBody(ReviewDecisionKind.Approve));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = JsonSerializer.Deserialize<AgentRunDto>(await response.Content.ReadAsStringAsync(), JsonOpts);
        Assert.NotNull(dto);
        Assert.Equal(AgentRunStatus.Completed, dto!.Status);
    }

    [Fact]
    public async Task Reject_MultiStepRun_FailsRunAndReturns200()
    {
        var run = BuildRunInRequiresReview(DateTimeOffset.UtcNow);
        _factory.Repository.Seed(run);

        using var client = _factory.CreateClient();
        var response = await client.PostAsync(
            $"/api/v1/agent-runs/{run.Id}/human-review",
            ReviewBody(ReviewDecisionKind.Reject, "Not authorized."));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = JsonSerializer.Deserialize<AgentRunDto>(await response.Content.ReadAsStringAsync(), JsonOpts);
        Assert.NotNull(dto);
        Assert.Equal(AgentRunStatus.Failed, dto!.Status);
    }

    [Fact]
    public async Task RequestChanges_MultiStepRun_LeavesRunInRequiresReviewAndReturns200()
    {
        var run = BuildRunInRequiresReview(DateTimeOffset.UtcNow);
        _factory.Repository.Seed(run);

        using var client = _factory.CreateClient();
        var response = await client.PostAsync(
            $"/api/v1/agent-runs/{run.Id}/human-review",
            ReviewBody(ReviewDecisionKind.RequestChanges, "Please adjust the tool inputs."));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = JsonSerializer.Deserialize<AgentRunDto>(await response.Content.ReadAsStringAsync(), JsonOpts);
        Assert.NotNull(dto);
        Assert.Equal(AgentRunStatus.RequiresReview, dto!.Status);
    }

    [Fact]
    public async Task Escalate_MultiStepRun_LeavesRunInRequiresReviewAndReturns200()
    {
        var run = BuildRunInRequiresReview(DateTimeOffset.UtcNow);
        _factory.Repository.Seed(run);

        using var client = _factory.CreateClient();
        var response = await client.PostAsync(
            $"/api/v1/agent-runs/{run.Id}/human-review",
            ReviewBody(ReviewDecisionKind.Escalate, "Escalated to senior reviewer."));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = JsonSerializer.Deserialize<AgentRunDto>(await response.Content.ReadAsStringAsync(), JsonOpts);
        Assert.NotNull(dto);
        Assert.Equal(AgentRunStatus.RequiresReview, dto!.Status);
    }

    [Fact]
    public async Task HumanReview_OnCompletedRun_Returns409Conflict()
    {
        var now = DateTimeOffset.UtcNow;
        // Use Reconstitute to create a run in Completed state without requiring steps
        var run = AgentRun.Reconstitute(
            Guid.NewGuid(), Guid.NewGuid(), "Agent", "obj", "trace-409-gov",
            AgentRunStatus.Completed, now, now, null,
            Array.Empty<AgentStep>(), Array.Empty<ExecutionTraceEvent>());
        _factory.Repository.Seed(run);

        using var client = _factory.CreateClient();
        var response = await client.PostAsync(
            $"/api/v1/agent-runs/{run.Id}/human-review",
            ReviewBody(ReviewDecisionKind.Approve));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task HumanReview_OnUnknownRun_Returns404NotFound()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsync(
            $"/api/v1/agent-runs/{Guid.NewGuid()}/human-review",
            ReviewBody(ReviewDecisionKind.Approve));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

public sealed class GovernanceResumeApiFixture : WebApplicationFactory<Program>
{
    public TestAgentRunRepository Repository { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAgentRunRepository>();
            services.AddSingleton<IAgentRunRepository>(Repository);
        });
    }
}
