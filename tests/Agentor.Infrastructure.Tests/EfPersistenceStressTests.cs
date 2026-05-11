using Agentor.Application;
using Agentor.Application.Abstractions;
using Agentor.Application.Commands;
using Agentor.Application.Options;
using Agentor.Application.Queries;
using Agentor.Application.RunQueue;
using Agentor.Domain;
using Agentor.Domain.Enums;
using Agentor.Domain.Governance;
using Agentor.Domain.Policy;
using Agentor.Infrastructure.Persistence;
using Agentor.Infrastructure.RunQueue;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Agentor.Infrastructure.Tests;

/// <summary>Phase 39 PR161 — persistence shape stress (local/dev; not scalability proof).</summary>
public sealed class EfPersistenceStressTests
{
    private static AgentorDbContext CreateContext(string dbName)
    {
        var opts = new DbContextOptionsBuilder<AgentorDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new AgentorDbContext(opts);
    }

    [Fact]
    public async Task SaveAsync_ManyTraceEvents_RoundTrips()
    {
        await using var ctx = CreateContext("stress-trace-" + Guid.NewGuid().ToString("N"));
        var repo = new EfCoreAgentRunRepository(ctx);
        var run = BuildRunWithTraces(traceCount: 220);
        var expectedTraceCount = run.Trace.Count;
        await repo.SaveAsync(run, CancellationToken.None);

        var loaded = await repo.GetAsync(run.Id, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal(expectedTraceCount, loaded!.Trace.Count);
    }

    [Fact]
    public async Task SaveAsync_ManyToolCallsAndPolicyDecisions_RoundTrips()
    {
        await using var ctx = CreateContext("stress-tools-" + Guid.NewGuid().ToString("N"));
        var repo = new EfCoreAgentRunRepository(ctx);
        var run = BuildRunWithToolsAndPolicy(toolCount: 35, decisionsPerTool: 2);
        await repo.SaveAsync(run, CancellationToken.None);

        var loaded = await repo.GetAsync(run.Id, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal(35, loaded!.Steps[0].ToolCalls.Count);
        Assert.Equal(70, loaded.Steps[0].PolicyDecisions.Count);
    }

    [Fact]
    public async Task SaveAsync_LargeResumeCursorJson_RoundTrips()
    {
        await using var ctx = CreateContext("stress-cursor-" + Guid.NewGuid().ToString("N"));
        var repo = new EfCoreAgentRunRepository(ctx);
        var run = AgentRun.Start(Guid.NewGuid(), "Stress", "o", "stress-cursor", DateTimeOffset.UtcNow);
        var step = run.StartStep("s", DateTimeOffset.UtcNow);
        run.EnterRequiresReview("stress", DateTimeOffset.UtcNow);

        var bigParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["blob"] = new string('z', 2000),
        };
        var remaining = new List<PendingPlanStep>();
        for (var i = 0; i < 40; i++)
        {
            remaining.Add(new PendingPlanStep(
                Guid.NewGuid(),
                $"src-{i}",
                i + 1,
                WellKnownToolKeys.Pr1FakeTool,
                RecipeStepKind.Tool,
                FailureHandlingPolicy.FailFast,
                bigParams,
                null));
        }

        var cursor = new PlanResumeCursor(
            Guid.NewGuid(),
            step.Id,
            "blocked-src",
            WellKnownToolKeys.Pr1FakeTool,
            remaining,
            [],
            DateTimeOffset.UtcNow,
            null);
        run.RecordPlanResumeCursor(cursor, DateTimeOffset.UtcNow);

        await repo.SaveAsync(run, CancellationToken.None);
        var loaded = await repo.GetAsync(run.Id, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.NotNull(loaded!.ResumeCursor);
        Assert.Equal(40, loaded.ResumeCursor!.RemainingSteps.Count);
    }

    [Fact]
    public async Task GetRunAuditExport_OnHeavyRun_Completes()
    {
        await using var ctx = CreateContext("stress-audit-" + Guid.NewGuid().ToString("N"));
        var repo = new EfCoreAgentRunRepository(ctx);
        var run = BuildRunWithTraces(80);
        await repo.SaveAsync(run, CancellationToken.None);

        var handler = new GetRunAuditExportQueryHandler(repo, Microsoft.Extensions.Options.Options.Create(new AuditExportOptions()));
        var export = await handler.HandleAsync(run.Id, CancellationToken.None);
        Assert.NotNull(export);
        Assert.Contains(run.Id.ToString("D"), export!.CanonicalJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InMemoryDurableQueue_EnqueueMany_ListLatest_ReturnsAll()
    {
        var store = new InMemoryDurableRunQueueStore();
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 120; i++)
        {
            var wi = new RunWorkItem(Guid.NewGuid(), new StartAgentRunCommand("StressQ", $"item-{i}"));
            await store.EnqueueAsync(wi, now.AddMilliseconds(i), CancellationToken.None);
        }

        var list = await store.ListLatestAsync(2000, CancellationToken.None);
        Assert.Equal(120, list.Count);
    }

    private static AgentRun BuildRunWithTraces(int traceCount)
    {
        var run = AgentRun.Start(Guid.NewGuid(), "T", "o", "stress-tr", DateTimeOffset.UtcNow);
        var step = run.StartStep("only", DateTimeOffset.UtcNow);
        for (var i = 0; i < traceCount; i++)
        {
            run.RecordTrace(TraceEventKind.ToolCallStarted, $"msg-{i}", DateTimeOffset.UtcNow.AddTicks(i));
        }

        step.Complete(DateTimeOffset.UtcNow);
        run.Complete(DateTimeOffset.UtcNow);
        return run;
    }

    private static AgentRun BuildRunWithToolsAndPolicy(int toolCount, int decisionsPerTool)
    {
        var clock = new SystemClock();
        var now = clock.UtcNow;
        var run = AgentRun.Start(Guid.NewGuid(), "TP", "o", "stress-tp", now);
        var step = run.StartStep("multi", now);
        for (var t = 0; t < toolCount; t++)
        {
            var tc = ToolCall.Start(run.Id, step.Id, WellKnownToolKeys.Pr1FakeTool, new Dictionary<string, string>(), now);
            step.AddToolCall(tc);
            for (var d = 0; d < decisionsPerTool; d++)
            {
                step.AddPolicyDecision(
                    new PolicyDecision(
                        Guid.NewGuid(),
                        run.Id,
                        step.Id,
                        PolicyDecisionOutcome.Allow,
                        "OK",
                        "ok",
                        now));
            }

            tc.Succeed(new Dictionary<string, string> { ["i"] = t.ToString() }, now);
        }

        step.Complete(now);
        run.Complete(now);
        return run;
    }
}
