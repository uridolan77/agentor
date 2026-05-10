using Agentor.Application.Abstractions;
using Agentor.Application.Coordination;
using Agentor.Application.Evaluation;
using Agentor.Application.Tests;
using Agentor.Domain;
using Agentor.Domain.Enums;
using Agentor.Infrastructure;
using Microsoft.Extensions.Options;
using Xunit;

namespace Agentor.Application.Tests.Evaluation;

public sealed class RunEvaluationHarnessTests
{
    private const string FakeTool = WellKnownToolKeys.Pr1FakeTool;

    [Fact]
    public async Task ExecutePlanAsync_CapturesSnapshotMetrics()
    {
        var clock = new SystemClock();
        var run = AgentRun.Start(Guid.NewGuid(), "Eval", "obj", "trace-eval", clock.UtcNow);
        var ok = AgentRecipe.TryCreate(
            Guid.NewGuid(),
            "one",
            AgentRecipeVersion.Parse("1"),
            CoordinationTopology.SequentialPipeline,
            [new RecipeStepDefinition("s1", 1, RecipeStepKind.Tool, FakeTool)],
            null,
            out var recipe,
            out _);
        Assert.True(ok);
        var plan = AgentPlan.Instantiate(recipe!, Guid.NewGuid(), clock.UtcNow);

        var registry = new ToolRegistry();
        registry.Register(new ToolDefinition(FakeTool, "t", "d", ToolRiskLevel.Low), new EchoExecutor());
        var policy = new RuntimePolicyEvaluator(registry, clock, Microsoft.Extensions.Options.Options.Create(new RuntimePolicyOptions()));
        var executor = AgentorTestComposition.CreateSequentialPlanExecutor(registry, policy, clock);

        var snap = await RunEvaluationHarness.ExecutePlanAsync(executor, run, plan, CancellationToken.None);

        Assert.Equal(AgentRunStatus.Completed, snap.RunStatus);
        Assert.True(snap.TraceEventCount > 0);
        Assert.Equal(1, snap.ToolCallCount);
        Assert.Equal(1, snap.PlanStepCount);
    }

    private sealed class EchoExecutor : IToolExecutor
    {
        public Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new ToolExecutionResult(true, new Dictionary<string, string>(), null));
        }
    }
}