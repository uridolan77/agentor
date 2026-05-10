using Agentor.Application;
using Agentor.Application.Quality;
using Agentor.Domain;
using Agentor.Domain.Enums;
using Agentor.Infrastructure;
using Xunit;

namespace Agentor.Application.Tests.Quality;

public sealed class RunQualityGateEvaluatorTests
{
    [Fact]
    public void Evaluate_CompletedRunWithRunCompletedTrace_Passes()
    {
        var run = AgentRun.Start(Guid.NewGuid(), "a", "o", "t", DateTimeOffset.UtcNow);
        run.StartStep("only", DateTimeOffset.UtcNow).Complete(DateTimeOffset.UtcNow);
        run.Complete(DateTimeOffset.UtcNow);
        var summary = RunQualityGateEvaluator.Evaluate(run);
        Assert.True(summary.Passed);
        Assert.Empty(summary.Violations);
        Assert.Empty(summary.Warnings);
    }

    [Fact]
    public void Evaluate_FailedRun_FailsWhenCompletionRequired()
    {
        var run = AgentRun.Start(Guid.NewGuid(), "a", "o", "t", DateTimeOffset.UtcNow);
        run.Fail("x", DateTimeOffset.UtcNow);
        var summary = RunQualityGateEvaluator.Evaluate(run);
        Assert.False(summary.Passed);
        Assert.Contains("RUN_FAILED", summary.Violations);
    }

    [Fact]
    public void Evaluate_RequiresReview_FailsWhenCompletionRequired()
    {
        var run = AgentRun.Start(Guid.NewGuid(), "a", "o", "t", DateTimeOffset.UtcNow);
        run.EnterRequiresReview("needs human", DateTimeOffset.UtcNow);
        var summary = RunQualityGateEvaluator.Evaluate(run);
        Assert.False(summary.Passed);
        Assert.Contains("RUN_REQUIRES_REVIEW", summary.Violations);
    }

    [Fact]
    public void Evaluate_CompletedWithoutRunCompletedTrace_Fails()
    {
        var runId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var trace = new List<ExecutionTraceEvent>
        {
            new(Guid.NewGuid(), runId, TraceEventKind.RunStarted, "Agent run started.", now)
        };
        var run = AgentRun.Reconstitute(
            runId,
            profileId,
            "a",
            "o",
            "t",
            AgentRunStatus.Completed,
            now,
            now,
            null,
            [],
            trace);
        var summary = RunQualityGateEvaluator.Evaluate(run);
        Assert.False(summary.Passed);
        Assert.Contains("MISSING_RUN_COMPLETED_TRACE", summary.Violations);
    }

    [Fact]
    public void Evaluate_CompletedWithFailedPlanStep_EmitsWarningWhenPlanProvided()
    {
        var clock = new SystemClock();
        var ok = AgentRecipe.TryCreate(
            Guid.NewGuid(),
            "two",
            AgentRecipeVersion.Parse("1"),
            CoordinationTopology.SequentialPipeline,
            [
                new RecipeStepDefinition("s1", 1, RecipeStepKind.Tool, WellKnownToolKeys.Pr1FakeTool),
                new RecipeStepDefinition("s2", 2, RecipeStepKind.Tool, WellKnownToolKeys.Pr1FakeTool)
            ],
            null,
            out var recipe,
            out _);
        Assert.True(ok);
        var plan = AgentPlan.Instantiate(recipe!, Guid.NewGuid(), clock.UtcNow);
        plan.Steps[0].Status = AgentPlanStepStatus.Failed;
        plan.Steps[1].Status = AgentPlanStepStatus.Completed;

        var runId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var trace = new List<ExecutionTraceEvent>
        {
            new(Guid.NewGuid(), runId, TraceEventKind.RunStarted, "Agent run started.", now),
            new(Guid.NewGuid(), runId, TraceEventKind.RunCompleted, "Agent run completed.", now)
        };
        var run = AgentRun.Reconstitute(
            runId,
            Guid.NewGuid(),
            "a",
            "o",
            "t",
            AgentRunStatus.Completed,
            now,
            now,
            null,
            [],
            trace);

        var summary = RunQualityGateEvaluator.Evaluate(run, plan: plan);
        Assert.True(summary.Passed);
        Assert.Contains("COMPLETED_RUN_WITH_FAILED_PLAN_STEP", summary.Warnings);
    }
}
