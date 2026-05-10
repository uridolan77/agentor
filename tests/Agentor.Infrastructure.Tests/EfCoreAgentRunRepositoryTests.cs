using Agentor.Application.Commands;
using Agentor.Domain;
using Agentor.Domain.Enums;
using Agentor.Domain.Governance;
using Agentor.Infrastructure;
using Agentor.Infrastructure.Conexus;
using Agentor.Infrastructure.Mcp;
using Agentor.Infrastructure.ExternalAgents;
using Agentor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Agentor.Infrastructure.Tests;

public sealed class EfCoreAgentRunRepositoryTests
{
    private static AgentorDbContext CreateContext(string dbName)
    {
        var opts = new DbContextOptionsBuilder<AgentorDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new AgentorDbContext(opts);
    }

    private static AgentRun BuildCompletedRun(string traceId = "ef-test-trace")
    {
        var profile = AgentProfile.Create("EF Agent", "Test via EF Core.", DateTimeOffset.UtcNow);
        var run = AgentRun.Start(profile.Id, profile.Name, "Verify EF round-trip.", traceId, DateTimeOffset.UtcNow);
        var step = run.StartStep("EF step", DateTimeOffset.UtcNow);
        step.Complete(DateTimeOffset.UtcNow);
        run.Complete(DateTimeOffset.UtcNow);
        return run;
    }

    [Fact]
    public void DbContext_CanBeCreated_WithInMemoryProvider()
    {
        using var ctx = CreateContext("model-compilation-test");
        Assert.NotNull(ctx);
    }

    [Fact]
    public async Task SaveAsync_PersistsRun_GetAsync_ReturnsIt()
    {
        await using var ctx = CreateContext("save-get-test");
        var repo = new EfCoreAgentRunRepository(ctx);

        var run = BuildCompletedRun("round-trip-trace");
        await repo.SaveAsync(run, CancellationToken.None);

        var loaded = await repo.GetAsync(run.Id, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(run.Id, loaded!.Id);
        Assert.Equal(run.ProfileId, loaded.ProfileId);
        Assert.Equal("round-trip-trace", loaded.TraceId);
        Assert.Equal(AgentRunStatus.Completed, loaded.Status);
        Assert.NotNull(loaded.CompletedAt);
    }

    [Fact]
    public async Task SaveAsync_PersistsSteps_GetAsync_ReturnsSteps()
    {
        await using var ctx = CreateContext("steps-test");
        var repo = new EfCoreAgentRunRepository(ctx);

        var run = BuildCompletedRun();
        await repo.SaveAsync(run, CancellationToken.None);

        var loaded = await repo.GetAsync(run.Id, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(run.Steps.Count, loaded!.Steps.Count);
        Assert.Equal(AgentStepStatus.Completed, loaded.Steps[0].Status);
    }

    [Fact]
    public async Task GetAsync_UnknownId_ReturnsNull()
    {
        await using var ctx = CreateContext("notfound-test");
        var repo = new EfCoreAgentRunRepository(ctx);

        var result = await repo.GetAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task SaveAsync_CalledTwiceWithSameId_OverwritesRun()
    {
        await using var ctx = CreateContext("overwrite-test");
        var repo = new EfCoreAgentRunRepository(ctx);

        var run = BuildCompletedRun("overwrite-trace");
        await repo.SaveAsync(run, CancellationToken.None);

        // Simulate saving again (e.g. after a failure path update)
        await repo.SaveAsync(run, CancellationToken.None);

        var loaded = await repo.GetAsync(run.Id, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal(run.Id, loaded!.Id);
    }

    [Fact]
    public async Task SaveAsync_StartAgentRunHandler_RoundTripsPolicyAndToolCalls()
    {
        await using var ctx = CreateContext("handler-policy-toolcall-test");
        await ctx.Database.EnsureCreatedAsync();
        var repo = new EfCoreAgentRunRepository(ctx);

        var clock = new SystemClock();
        var fake = new FakeToolExecutor();
        var registry = ToolRegistry.CreateDefault(fake, new FakeModelGatewayClient(), new FakeMcpRegistryClient(), new FakeA2AExternalAgentClient());
        var policy = new RuntimePolicyEvaluator(registry, clock, Microsoft.Extensions.Options.Options.Create(new RuntimePolicyOptions()));
        var pipeline = new ToolExecutionPipeline(clock, Microsoft.Extensions.Options.Options.Create(new ToolExecutionOptions()));
        var handler = new StartAgentRunHandler(repo, policy, registry, pipeline, clock);

        var run = await handler.HandleAsync(
            new StartAgentRunCommand("Handler EF Agent", "Policy/tool EF round-trip.", "handler-ef-trace"),
            CancellationToken.None);

        Assert.Single(run.Steps[0].PolicyDecisions);
        Assert.Single(run.Steps[0].ToolCalls);
        Assert.Equal(PolicyDecisionOutcome.Allow, run.Steps[0].PolicyDecisions[0].Outcome);

        var loaded = await repo.GetAsync(run.Id, CancellationToken.None);
        Assert.NotNull(loaded);
        var step = loaded!.Steps[0];
        Assert.Single(step.PolicyDecisions);
        Assert.Single(step.ToolCalls);
        Assert.Equal("pr1.fake-tool", step.ToolCalls[0].ToolKey);
        Assert.Equal(PolicyDecisionOutcome.Allow, step.PolicyDecisions[0].Outcome);
    }

    [Fact]
    public async Task SaveAsync_RoundTripsGovernanceScopeFields_FromStartCommand()
    {
        await using var ctx = CreateContext("governance-scope-test");
        await ctx.Database.EnsureCreatedAsync();
        var repo = new EfCoreAgentRunRepository(ctx);
        var clock = new SystemClock();
        var fake = new FakeToolExecutor();
        var registry = ToolRegistry.CreateDefault(fake, new FakeModelGatewayClient(), new FakeMcpRegistryClient(), new FakeA2AExternalAgentClient());
        var policy = new RuntimePolicyEvaluator(registry, clock, Microsoft.Extensions.Options.Options.Create(new RuntimePolicyOptions()));
        var pipeline = new ToolExecutionPipeline(clock, Microsoft.Extensions.Options.Options.Create(new ToolExecutionOptions()));
        var handler = new StartAgentRunHandler(repo, policy, registry, pipeline, clock);

        var tenantId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var knowledgeScopeId = Guid.NewGuid();

        var run = await handler.HandleAsync(
            new StartAgentRunCommand(
                "Scoped Agent",
                "Governance scope persistence.",
                "scope-trace",
                tenantId,
                workspaceId,
                projectId,
                knowledgeScopeId),
            CancellationToken.None);

        var loaded = await repo.GetAsync(run.Id, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal(tenantId, loaded!.TenantId);
        Assert.Equal(workspaceId, loaded.WorkspaceId);
        Assert.Equal(projectId, loaded.ProjectId);
        Assert.Equal(knowledgeScopeId, loaded.KnowledgeScopeId);
    }

    [Fact]
    public async Task SaveAsync_RoundTripsHumanReviewDecisions()
    {
        await using var ctx = CreateContext("human-review-json-test");
        await ctx.Database.EnsureCreatedAsync();
        var repo = new EfCoreAgentRunRepository(ctx);
        var clock = new SystemClock();
        var now = clock.UtcNow;
        var profileId = Guid.NewGuid();
        var decision = new HumanReviewDecision(
            Guid.NewGuid(),
            ReviewDecisionKind.RequestChanges,
            Guid.NewGuid(),
            now,
            "note",
            ReviewResolutionStatus.ChangesRequested);
        var run = AgentRun.Reconstitute(
            Guid.NewGuid(),
            profileId,
            "Agent",
            "Objective",
            "hr-json-trace",
            AgentRunStatus.RequiresReview,
            now.AddMinutes(-1),
            now,
            "pending",
            [],
            [],
            null,
            null,
            null,
            null,
            null,
            [decision]);

        await repo.SaveAsync(run, CancellationToken.None);
        var loaded = await repo.GetAsync(run.Id, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Single(loaded!.HumanReviewDecisions);
        Assert.Equal(decision.Id, loaded.HumanReviewDecisions[0].Id);
        Assert.Equal(ReviewDecisionKind.RequestChanges, loaded.HumanReviewDecisions[0].Kind);
    }

    [Fact]
    public async Task SaveAsync_PersistsTraceEvents()
    {
        await using var ctx = CreateContext("trace-test");
        var repo = new EfCoreAgentRunRepository(ctx);

        var run = BuildCompletedRun();
        await repo.SaveAsync(run, CancellationToken.None);

        var loaded = await repo.GetAsync(run.Id, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.True(loaded!.Trace.Count >= 1);
        Assert.Equal(run.Trace.Count, loaded.Trace.Count);
    }

    [Fact]
    public async Task ListSummariesAsync_ReturnsNewestFirst_WithPaging()
    {
        await using var ctx = CreateContext("list-summaries-test");
        var repo = new EfCoreAgentRunRepository(ctx);

        var run1 = BuildCompletedRun("list-trace-1");
        await Task.Delay(5);
        var run2 = BuildCompletedRun("list-trace-2");

        await repo.SaveAsync(run1, CancellationToken.None);
        await repo.SaveAsync(run2, CancellationToken.None);

        var page = await repo.ListSummariesAsync(0, 10, CancellationToken.None);

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(2, page.Items.Count);
        Assert.True(page.Items[0].StartedAt >= page.Items[1].StartedAt);
    }
}
