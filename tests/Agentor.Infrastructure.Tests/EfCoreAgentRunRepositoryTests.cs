using Agentor.Domain;
using Agentor.Domain.Enums;
using Agentor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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
    public async Task SaveAsync_PersistsPolicyDecisionsAndToolCalls()
    {
        await using var ctx = CreateContext("policy-toolcall-test");
        var repo = new EfCoreAgentRunRepository(ctx);

        var run = BuildCompletedRun();
        var originalStep = run.Steps[0];

        await repo.SaveAsync(run, CancellationToken.None);
        var loaded = await repo.GetAsync(run.Id, CancellationToken.None);

        Assert.NotNull(loaded);
        var step = loaded!.Steps[0];

        Assert.Equal(originalStep.PolicyDecisions.Count, step.PolicyDecisions.Count);
        Assert.Equal(originalStep.ToolCalls.Count, step.ToolCalls.Count);
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
}
