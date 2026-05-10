using System.Text.Json;
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

    private sealed class HarnessFixtureRoot
    {
        public int SchemaVersion { get; set; }
        public string? Kind { get; set; }
        public string? AgentName { get; set; }
        public string? Objective { get; set; }
        public string? TraceId { get; set; }
        public string? RecipeName { get; set; }
        public string? RecipeVersion { get; set; }
        public string? ToolStepId { get; set; }
        public int ToolStepOrder { get; set; }
        public string? ToolKey { get; set; }
        public SnapshotExpectation? ExpectedSnapshot { get; set; }
    }

    private sealed class SnapshotExpectation
    {
        public string? RunStatus { get; set; }
        public int TraceEventCount { get; set; }
        public int ToolCallCount { get; set; }
        public int PlanStepCount { get; set; }
    }

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

    [Fact]
    public async Task ExecutePlanAsync_MatchesEvaluationHarnessFixture()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "eval", "evaluation-harness-one-step-tool.json");
        Assert.True(File.Exists(path), $"Missing fixture: {path}");
        var json = await File.ReadAllTextAsync(path);
        var root = JsonSerializer.Deserialize<HarnessFixtureRoot>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(root);
        Assert.Equal(2, root!.SchemaVersion);
        Assert.Equal("RunEvaluationHarness", root.Kind);
        Assert.NotNull(root.ExpectedSnapshot);

        var clock = new SystemClock();
        var run = AgentRun.Start(
            Guid.NewGuid(),
            root.AgentName ?? "Eval",
            root.Objective ?? "obj",
            root.TraceId ?? "trace",
            clock.UtcNow);
        var ok = AgentRecipe.TryCreate(
            Guid.NewGuid(),
            root.RecipeName ?? "one",
            AgentRecipeVersion.Parse(root.RecipeVersion ?? "1"),
            CoordinationTopology.SequentialPipeline,
            [new RecipeStepDefinition(root.ToolStepId ?? "s1", root.ToolStepOrder, RecipeStepKind.Tool, root.ToolKey ?? FakeTool)],
            null,
            out var recipe,
            out _);
        Assert.True(ok);
        var plan = AgentPlan.Instantiate(recipe!, Guid.NewGuid(), clock.UtcNow);

        var registry = new ToolRegistry();
        registry.Register(new ToolDefinition(root.ToolKey ?? FakeTool, "t", "d", ToolRiskLevel.Low), new EchoExecutor());
        var policy = new RuntimePolicyEvaluator(registry, clock, Microsoft.Extensions.Options.Options.Create(new RuntimePolicyOptions()));
        var executor = AgentorTestComposition.CreateSequentialPlanExecutor(registry, policy, clock);

        var snap = await RunEvaluationHarness.ExecutePlanAsync(executor, run, plan, CancellationToken.None);
        var exp = root.ExpectedSnapshot!;
        Assert.Equal(Enum.Parse<AgentRunStatus>(exp.RunStatus!, ignoreCase: true), snap.RunStatus);
        Assert.Equal(exp.TraceEventCount, snap.TraceEventCount);
        Assert.Equal(exp.ToolCallCount, snap.ToolCallCount);
        Assert.Equal(exp.PlanStepCount, snap.PlanStepCount);
    }

    private sealed class EchoExecutor : IToolExecutor
    {
        public Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new ToolExecutionResult(true, new Dictionary<string, string>(), null));
        }
    }
}