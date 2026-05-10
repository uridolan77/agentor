using Agentor.Application;
using Agentor.Application.Abstractions;
using Agentor.Domain;
using Agentor.Domain.Enums;
using Agentor.Infrastructure;
using Xunit;

namespace Agentor.Application.Tests;

public sealed class SequentialAgentPlanExecutorTests
{
// Contract: AgentRunStatus.Completed and AgentPlanExecutionResult.Success do not imply every plan step
// succeeded when FailureHandlingPolicy.ContinueOnFailure is used. Inspect StepResults and PlanStatus.
    private const string FakeTool = WellKnownToolKeys.Pr1FakeTool;

    private static AgentRecipe CreateTwoStepRecipe(FailureHandlingPolicy s1 = FailureHandlingPolicy.FailFast, FailureHandlingPolicy s2 = FailureHandlingPolicy.FailFast)
    {
        var ok = AgentRecipe.TryCreate(
            Guid.NewGuid(),
            "two-step",
            AgentRecipeVersion.Parse("1.0"),
            CoordinationTopology.SequentialPipeline,
            [
                new RecipeStepDefinition("s1", 1, RecipeStepKind.Tool, FakeTool, OnFailure: s1),
                new RecipeStepDefinition("s2", 2, RecipeStepKind.Tool, FakeTool, OnFailure: s2)
            ],
            null,
            out var recipe,
            out _);
        Assert.True(ok);
        Assert.NotNull(recipe);
        return recipe;
    }

    [Fact]
    public async Task TwoStepPlan_ExecutesInOrder_UsesRegistryPolicyAndPipeline()
    {
        var clock = new SystemClock();
        var run = AgentRun.Start(Guid.NewGuid(), "PlanTest", "obj", "trace-plan", clock.UtcNow);
        var recipe = CreateTwoStepRecipe();
        var plan = AgentPlan.Instantiate(recipe, Guid.NewGuid(), clock.UtcNow);

        var counting = new CountingExecutor();
        var registry = new ToolRegistry();
        registry.Register(new ToolDefinition(FakeTool, "t", "d", ToolRiskLevel.Low), counting);
        var policy = new RuntimePolicyEvaluator(registry, clock, Microsoft.Extensions.Options.Options.Create(new RuntimePolicyOptions()));
        var executor = AgentorTestComposition.CreateSequentialPlanExecutor(registry, policy, clock);

        var result = await executor.ExecuteAsync(run, plan, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(AgentRunStatus.Completed, run.Status);
        Assert.Equal(2, counting.Invocations);
        Assert.Contains(run.Trace, e => e.Kind == TraceEventKind.PlanExecutionStarted);
        Assert.Contains(run.Trace, e => e.Kind == TraceEventKind.PlanExecutionCompleted);
    }

    [Fact]
    public async Task DeniedFirstStep_DoesNotInvokeSecondExecutor()
    {
        var clock = new SystemClock();
        var run = AgentRun.Start(Guid.NewGuid(), "PlanTest", "obj", "trace-deny", clock.UtcNow);
        var recipe = CreateTwoStepRecipe();
        var plan = AgentPlan.Instantiate(recipe, Guid.NewGuid(), clock.UtcNow);

        var counting = new CountingExecutor();
        var registry = new ToolRegistry();
        registry.Register(new ToolDefinition(FakeTool, "t", "d", ToolRiskLevel.Low), counting);
        var policy = new RuntimePolicyEvaluator(
            registry,
            clock,
            Microsoft.Extensions.Options.Options.Create(new RuntimePolicyOptions { DeniedToolKeys = [FakeTool] }));
        var executor = AgentorTestComposition.CreateSequentialPlanExecutor(registry, policy, clock);

        var result = await executor.ExecuteAsync(run, plan, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(0, counting.Invocations);
        Assert.Equal(AgentRunStatus.Failed, run.Status);
    }

    [Fact]
    public async Task RequiresReviewFirstStep_DoesNotExecute_AndIsDistinctFromDeny()
    {
        var clock = new SystemClock();
        var run = AgentRun.Start(Guid.NewGuid(), "PlanTest", "obj", "trace-rev", clock.UtcNow);
        var recipe = CreateTwoStepRecipe();
        var plan = AgentPlan.Instantiate(recipe, Guid.NewGuid(), clock.UtcNow);

        var counting = new CountingExecutor();
        var registry = new ToolRegistry();
        registry.Register(new ToolDefinition(FakeTool, "t", "d", ToolRiskLevel.High), counting);
        var policy = new RuntimePolicyEvaluator(
            registry,
            clock,
            Microsoft.Extensions.Options.Options.Create(new RuntimePolicyOptions { MaxAutoApproveRisk = nameof(ToolRiskLevel.Low) }));
        var executor = AgentorTestComposition.CreateSequentialPlanExecutor(registry, policy, clock);

        var result = await executor.ExecuteAsync(run, plan, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(AgentRunStatus.RequiresReview, run.Status);
        Assert.Equal(0, counting.Invocations);
        Assert.Equal(PolicyDecisionOutcome.RequiresReview, run.Steps[0].PolicyDecisions[0].Outcome);
    }

    [Fact]
    public async Task AlwaysGuard_ExecutesBothSteps()
    {
        var clock = new SystemClock();
        var run = AgentRun.Start(Guid.NewGuid(), "PlanTest", "obj", "trace-guard", clock.UtcNow);
        var ok = AgentRecipe.TryCreate(
            Guid.NewGuid(),
            "g",
            AgentRecipeVersion.Parse("1"),
            CoordinationTopology.SequentialPipeline,
            [
                new RecipeStepDefinition("a", 1, RecipeStepKind.Tool, FakeTool, Guard: new StepGuardDefinition(StepGuardKind.Always)),
                new RecipeStepDefinition("b", 2, RecipeStepKind.Tool, FakeTool, Guard: new StepGuardDefinition(StepGuardKind.Always))
            ],
            null,
            out var recipe,
            out _);
        Assert.True(ok);
        Assert.NotNull(recipe);
        var plan = AgentPlan.Instantiate(recipe, Guid.NewGuid(), clock.UtcNow);

        var counting = new CountingExecutor();
        var registry = new ToolRegistry();
        registry.Register(new ToolDefinition(FakeTool, "t", "d", ToolRiskLevel.Low), counting);
        var policy = new RuntimePolicyEvaluator(registry, clock, Microsoft.Extensions.Options.Options.Create(new RuntimePolicyOptions()));
        var executor = AgentorTestComposition.CreateSequentialPlanExecutor(registry, policy, clock);

        await executor.ExecuteAsync(run, plan, CancellationToken.None);

        Assert.Equal(2, counting.Invocations);
    }

    [Fact]
    public async Task PreviousStepFailedGuard_SecondStepRunsWhenFirstToolFails_AndContinues()
    {
        var clock = new SystemClock();
        var run = AgentRun.Start(Guid.NewGuid(), "PlanTest", "obj", "trace-pf", clock.UtcNow);
        var ok = AgentRecipe.TryCreate(
            Guid.NewGuid(),
            "pf",
            AgentRecipeVersion.Parse("1"),
            CoordinationTopology.SequentialPipeline,
            [
                new RecipeStepDefinition("a", 1, RecipeStepKind.Tool, FakeTool, OnFailure: FailureHandlingPolicy.ContinueOnFailure),
                new RecipeStepDefinition(
                    "b",
                    2,
                    RecipeStepKind.Tool,
                    FakeTool,
                    Guard: new StepGuardDefinition(StepGuardKind.PreviousStepFailed),
                    OnFailure: FailureHandlingPolicy.FailFast)
            ],
            null,
            out var recipe,
            out _);
        Assert.True(ok);
        Assert.NotNull(recipe);
        var plan = AgentPlan.Instantiate(recipe, Guid.NewGuid(), clock.UtcNow);

        var counting = new FailsOnceThenSucceedsExecutor();
        var registry = new ToolRegistry();
        registry.Register(new ToolDefinition(FakeTool, "t", "d", ToolRiskLevel.Low), counting);
        var policy = new RuntimePolicyEvaluator(registry, clock, Microsoft.Extensions.Options.Options.Create(new RuntimePolicyOptions()));
        var executor = AgentorTestComposition.CreateSequentialPlanExecutor(registry, policy, clock);

        await executor.ExecuteAsync(run, plan, CancellationToken.None);

        Assert.Equal(2, counting.Invocations);
        Assert.Equal(AgentRunStatus.Completed, run.Status);
    }

    [Fact]
    public async Task SkipRemaining_SkipsLaterPlanSteps()
    {
        var clock = new SystemClock();
        var run = AgentRun.Start(Guid.NewGuid(), "PlanTest", "obj", "trace-skip", clock.UtcNow);
        var ok = AgentRecipe.TryCreate(
            Guid.NewGuid(),
            "sk",
            AgentRecipeVersion.Parse("1"),
            CoordinationTopology.SequentialPipeline,
            [
                new RecipeStepDefinition("a", 1, RecipeStepKind.Tool, FakeTool, OnFailure: FailureHandlingPolicy.SkipRemaining),
                new RecipeStepDefinition("b", 2, RecipeStepKind.Tool, FakeTool)
            ],
            null,
            out var recipe,
            out _);
        Assert.True(ok);
        Assert.NotNull(recipe);
        var plan = AgentPlan.Instantiate(recipe, Guid.NewGuid(), clock.UtcNow);

        var counting = new FailsOnceThenSucceedsExecutor();
        var registry = new ToolRegistry();
        registry.Register(new ToolDefinition(FakeTool, "t", "d", ToolRiskLevel.Low), counting);
        var policy = new RuntimePolicyEvaluator(registry, clock, Microsoft.Extensions.Options.Options.Create(new RuntimePolicyOptions()));
        var executor = AgentorTestComposition.CreateSequentialPlanExecutor(
            registry,
            policy,
            clock,
            new ToolExecutionOptions { MaxAttempts = 1, TimeoutMilliseconds = 500 });

        await executor.ExecuteAsync(run, plan, CancellationToken.None);

        Assert.Equal(1, counting.Invocations);
        Assert.Equal(AgentPlanStepStatus.Skipped, plan.Steps[1].Status);
    }

    [Fact]
    public async Task MarkForCompensation_RecordsHookMetadata_WithoutExecutingCompensation()
    {
        var clock = new SystemClock();
        var run = AgentRun.Start(Guid.NewGuid(), "PlanTest", "obj", "trace-comp", clock.UtcNow);
        var hook = new CompensationHookDefinition("hook-1", "demo");
        var ok = AgentRecipe.TryCreate(
            Guid.NewGuid(),
            "c",
            AgentRecipeVersion.Parse("1"),
            CoordinationTopology.SequentialPipeline,
            [
                new RecipeStepDefinition(
                    "a",
                    1,
                    RecipeStepKind.Tool,
                    FakeTool,
                    OnFailure: FailureHandlingPolicy.MarkForCompensation,
                    Compensation: hook),
                new RecipeStepDefinition("b", 2, RecipeStepKind.Tool, FakeTool)
            ],
            null,
            out var recipe,
            out _);
        Assert.True(ok);
        Assert.NotNull(recipe);
        var plan = AgentPlan.Instantiate(recipe, Guid.NewGuid(), clock.UtcNow);

        var counting = new AlwaysFailExecutor();
        var registry = new ToolRegistry();
        registry.Register(new ToolDefinition(FakeTool, "t", "d", ToolRiskLevel.Low), counting);
        var policy = new RuntimePolicyEvaluator(registry, clock, Microsoft.Extensions.Options.Options.Create(new RuntimePolicyOptions()));
        var executor = AgentorTestComposition.CreateSequentialPlanExecutor(
            registry,
            policy,
            clock,
            new ToolExecutionOptions { MaxAttempts = 1, TimeoutMilliseconds = 500 });

        await executor.ExecuteAsync(run, plan, CancellationToken.None);

        Assert.Equal(CompensationStatus.Recorded, plan.Steps[0].CompensationStatus);
        Assert.Equal(2, counting.Invocations);
        Assert.Contains(run.Trace, e => e.Kind == TraceEventKind.CompensationHookRecorded);
    }


    [Fact]
    public async Task TwoStepPlanSuccess_PlanTraceKindsAppearInStrictCoordinatorOrder()
    {
        var clock = new SystemClock();
        var run = AgentRun.Start(Guid.NewGuid(), "PlanTest", "obj", "trace-order", clock.UtcNow);
        var recipe = CreateTwoStepRecipe();
        var plan = AgentPlan.Instantiate(recipe, Guid.NewGuid(), clock.UtcNow);

        var counting = new CountingExecutor();
        var registry = new ToolRegistry();
        registry.Register(new ToolDefinition(FakeTool, "t", "d", ToolRiskLevel.Low), counting);
        var policy = new RuntimePolicyEvaluator(registry, clock, Microsoft.Extensions.Options.Options.Create(new RuntimePolicyOptions()));
        var executor = AgentorTestComposition.CreateSequentialPlanExecutor(
            registry,
            policy,
            clock,
            new ToolExecutionOptions { MaxAttempts = 1, TimeoutMilliseconds = 500 });

        await executor.ExecuteAsync(run, plan, CancellationToken.None);

        var kinds = run.Trace.Select(e => e.Kind).Where(PlanCoordinationKinds.Contains).ToArray();
        var expected = new[]
        {
            TraceEventKind.PlanExecutionStarted,
            TraceEventKind.StepGuardEvaluated,
            TraceEventKind.PlanExecutionStepStarted,
            TraceEventKind.PolicyEvaluated,
            TraceEventKind.ToolCallStarted,
            TraceEventKind.ToolExecutionAttemptStarted,
            TraceEventKind.ToolExecutionAttemptFinished,
            TraceEventKind.PlanExecutionStepCompleted,
            TraceEventKind.StepGuardEvaluated,
            TraceEventKind.PlanExecutionStepStarted,
            TraceEventKind.PolicyEvaluated,
            TraceEventKind.ToolCallStarted,
            TraceEventKind.ToolExecutionAttemptStarted,
            TraceEventKind.ToolExecutionAttemptFinished,
            TraceEventKind.PlanExecutionStepCompleted,
            TraceEventKind.PlanExecutionCompleted,
        };
        Assert.Equal(expected, kinds);
    }

    [Fact]
    public async Task ContinueOnFailure_CompletedRun_PlanResultSuccess_WithFailedFirstPlanStep()
    {
        var clock = new SystemClock();
        var run = AgentRun.Start(Guid.NewGuid(), "PlanTest", "obj", "trace-cof-sem", clock.UtcNow);
        var ok = AgentRecipe.TryCreate(
            Guid.NewGuid(),
            "cof-sem",
            AgentRecipeVersion.Parse("1"),
            CoordinationTopology.SequentialPipeline,
            [
                new RecipeStepDefinition("a", 1, RecipeStepKind.Tool, FakeTool, OnFailure: FailureHandlingPolicy.ContinueOnFailure),
                new RecipeStepDefinition(
                    "b",
                    2,
                    RecipeStepKind.Tool,
                    FakeTool,
                    Guard: new StepGuardDefinition(StepGuardKind.PreviousStepFailed),
                    OnFailure: FailureHandlingPolicy.FailFast)
            ],
            null,
            out var recipe,
            out _);
        Assert.True(ok);
        Assert.NotNull(recipe);
        var plan = AgentPlan.Instantiate(recipe, Guid.NewGuid(), clock.UtcNow);

        var counting = new FailsOnceThenSucceedsExecutor();
        var registry = new ToolRegistry();
        registry.Register(new ToolDefinition(FakeTool, "t", "d", ToolRiskLevel.Low), counting);
        var policy = new RuntimePolicyEvaluator(registry, clock, Microsoft.Extensions.Options.Options.Create(new RuntimePolicyOptions()));
        var executor = AgentorTestComposition.CreateSequentialPlanExecutor(
            registry,
            policy,
            clock,
            new ToolExecutionOptions { MaxAttempts = 1, TimeoutMilliseconds = 500 });

        var result = await executor.ExecuteAsync(run, plan, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(AgentRunStatus.Completed, result.RunStatus);
        Assert.Equal(AgentPlanStatus.Failed, result.PlanStatus);
        Assert.Equal(2, counting.Invocations);

        var stepA = Assert.Single(result.StepResults, s => s.SourceStepId == "a");
        var stepB = Assert.Single(result.StepResults, s => s.SourceStepId == "b");
        Assert.Equal(AgentPlanStepStatus.Failed, stepA.FinalStatus);
        Assert.Equal(AgentPlanStepStatus.Completed, stepB.FinalStatus);
    }

    [Theory]
    [InlineData(FailureHandlingPolicy.FailFast, 1, AgentRunStatus.Failed, AgentPlanStepStatus.Failed, AgentPlanStepStatus.Pending)]
    [InlineData(FailureHandlingPolicy.ContinueOnFailure, 2, AgentRunStatus.Completed, AgentPlanStepStatus.Failed, AgentPlanStepStatus.Completed)]
    [InlineData(FailureHandlingPolicy.SkipRemaining, 1, AgentRunStatus.Completed, AgentPlanStepStatus.Failed, AgentPlanStepStatus.Skipped)]
    public async Task FirstStepToolFailure_OnFailurePolicyMatrix(
        FailureHandlingPolicy firstOnFailure,
        int expectedInvocations,
        AgentRunStatus expectedRunStatus,
        AgentPlanStepStatus expectedFirstStatus,
        AgentPlanStepStatus expectedSecondStatus)
    {
        var clock = new SystemClock();
        var run = AgentRun.Start(Guid.NewGuid(), "PlanTest", "obj", $"trace-matrix-{firstOnFailure}", clock.UtcNow);
        var ok = AgentRecipe.TryCreate(
            Guid.NewGuid(),
            "matrix",
            AgentRecipeVersion.Parse("1"),
            CoordinationTopology.SequentialPipeline,
            [
                new RecipeStepDefinition("a", 1, RecipeStepKind.Tool, FakeTool, OnFailure: firstOnFailure),
                new RecipeStepDefinition("b", 2, RecipeStepKind.Tool, FakeTool, Guard: new StepGuardDefinition(StepGuardKind.Always))
            ],
            null,
            out var recipe,
            out _);
        Assert.True(ok);
        Assert.NotNull(recipe);
        var plan = AgentPlan.Instantiate(recipe, Guid.NewGuid(), clock.UtcNow);

        IToolExecutor toolExecutor = firstOnFailure == FailureHandlingPolicy.FailFast
            ? new AlwaysFailExecutor()
            : new FailsOnceThenSucceedsExecutor();

        var registry = new ToolRegistry();
        registry.Register(new ToolDefinition(FakeTool, "t", "d", ToolRiskLevel.Low), toolExecutor);
        var policy = new RuntimePolicyEvaluator(registry, clock, Microsoft.Extensions.Options.Options.Create(new RuntimePolicyOptions()));
        var executor = AgentorTestComposition.CreateSequentialPlanExecutor(
            registry,
            policy,
            clock,
            new ToolExecutionOptions { MaxAttempts = 1, TimeoutMilliseconds = 500 });

        await executor.ExecuteAsync(run, plan, CancellationToken.None);

        var invocations = toolExecutor switch
        {
            AlwaysFailExecutor a => a.Invocations,
            FailsOnceThenSucceedsExecutor f => f.Invocations,
            _ => throw new InvalidOperationException()
        };
        Assert.Equal(expectedInvocations, invocations);
        Assert.Equal(expectedRunStatus, run.Status);
        Assert.Equal(expectedFirstStatus, plan.Steps[0].Status);
        Assert.Equal(expectedSecondStatus, plan.Steps[1].Status);
    }

    [Fact]
    public async Task SkillPlan_WithSegmentAndTool_RecordsSkillTraces_AndInvokesInnerTool()
    {
        const string skillKey = "harness.echo-skill";
        var skillVersion = AgentRecipeVersion.Parse("1.0.0");
        var okSkill = SkillPackage.TryCreate(
            Guid.NewGuid(),
            skillKey,
            skillVersion,
            "Echo skill",
            "Segment then echo tool.",
            [
                new SkillProcedureStepDefinition("p1", 1, "Intro", SkillProcedureStepKind.Segment),
                new SkillProcedureStepDefinition("p2", 2, "Echo", SkillProcedureStepKind.ToolRef, FakeTool)
            ],
            out var skillPkg,
            out var skillVal);
        Assert.True(okSkill);
        Assert.NotNull(skillPkg);
        Assert.True(skillVal.IsValid);

        var catalog = new InMemorySkillPackageCatalog();
        catalog.Register(skillPkg!);

        var clock = new SystemClock();
        var run = AgentRun.Start(Guid.NewGuid(), "SkillPlan", "obj", "trace-skill", clock.UtcNow);
        var okRecipe = AgentRecipe.TryCreate(
            Guid.NewGuid(),
            "one-skill",
            AgentRecipeVersion.Parse("1"),
            CoordinationTopology.SequentialPipeline,
            [
                new RecipeStepDefinition(
                    "s1",
                    1,
                    RecipeStepKind.Skill,
                    string.Empty,
                    InvokedSkillKey: skillKey,
                    InvokedSkillVersion: skillVersion)
            ],
            null,
            out var recipe,
            out var recipeVal);
        Assert.True(okRecipe);
        Assert.True(recipeVal.IsValid);
        var plan = AgentPlan.Instantiate(recipe!, Guid.NewGuid(), clock.UtcNow);

        var counting = new CountingExecutor();
        var registry = new ToolRegistry();
        registry.Register(new ToolDefinition(FakeTool, "t", "d", ToolRiskLevel.Low), counting);
        var policy = new RuntimePolicyEvaluator(registry, clock, Microsoft.Extensions.Options.Options.Create(new RuntimePolicyOptions()));
        var executor = AgentorTestComposition.CreateSequentialPlanExecutor(registry, policy, clock, skillCatalog: catalog);

        var result = await executor.ExecuteAsync(run, plan, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(AgentRunStatus.Completed, run.Status);
        Assert.Equal(1, counting.Invocations);

        var started = Assert.Single(run.Trace, e => e.Kind == TraceEventKind.SkillInvocationStarted);
        Assert.Equal(skillKey, started.Data["skillKey"]);
        Assert.Equal(skillVersion.Value, started.Data["skillVersion"]);

        var segment = Assert.Single(run.Trace, e => e.Kind == TraceEventKind.SkillProcedureSegmentRecorded);
        Assert.Equal("p1", segment.Data["procedureStepId"]);

        var innerPolicy = Assert.Single(run.Trace, e =>
            e.Kind == TraceEventKind.PolicyEvaluated && e.Message.Contains("skill inner", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("p2", innerPolicy.Data["procedureStepId"]);
        Assert.Equal(FakeTool, innerPolicy.Data["toolKey"]);

        var innerToolStart = Assert.Single(run.Trace, e =>
            e.Kind == TraceEventKind.ToolCallStarted && e.Message.Contains("skill inner", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("p2", innerToolStart.Data["procedureStepId"]);
        Assert.Equal(FakeTool, innerToolStart.Data["toolKey"]);

        Assert.Contains(run.Trace, e => e.Kind == TraceEventKind.SkillInvocationCompleted);

        var runStep = Assert.Single(run.Steps);
        var innerCall = Assert.Single(runStep.ToolCalls);
        Assert.Equal(FakeTool, innerCall.ToolKey);
        Assert.Equal(ToolCallStatus.Succeeded, innerCall.Status);
        Assert.Contains(runStep.PolicyDecisions, d => d.Outcome == PolicyDecisionOutcome.Allow);
    }

    private static readonly HashSet<TraceEventKind> PlanCoordinationKinds =
    [
        TraceEventKind.PlanExecutionStarted,
        TraceEventKind.StepGuardEvaluated,
        TraceEventKind.PlanStepSkipped,
        TraceEventKind.PlanExecutionStepStarted,
        TraceEventKind.PolicyEvaluated,
        TraceEventKind.ToolCallStarted,
        TraceEventKind.ToolExecutionAttemptStarted,
        TraceEventKind.ToolExecutionAttemptFinished,
        TraceEventKind.PlanFailureDecisionRecorded,
        TraceEventKind.PlanExecutionStepCompleted,
        TraceEventKind.PlanExecutionRequiresReview,
        TraceEventKind.PlanExecutionFailed,
        TraceEventKind.CompensationHookRecorded,
        TraceEventKind.PlanExecutionCompleted,
    ];
    private sealed class AlwaysFailExecutor : IToolExecutor
    {
        public int Invocations;

        public Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request, CancellationToken cancellationToken)
        {
            Invocations++;
            return Task.FromResult(new ToolExecutionResult(false, new Dictionary<string, string>(), "always fail"));
        }
    }

    private sealed class CountingExecutor : IToolExecutor
    {
        public int Invocations;

        public Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request, CancellationToken cancellationToken)
        {
            Invocations++;
            return Task.FromResult(new ToolExecutionResult(true, new Dictionary<string, string> { ["k"] = "v" }));
        }
    }

    private sealed class FailsOnceThenSucceedsExecutor : IToolExecutor
    {
        public int Invocations;

        public Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request, CancellationToken cancellationToken)
        {
            Invocations++;
            if (Invocations == 1)
            {
                return Task.FromResult(new ToolExecutionResult(false, new Dictionary<string, string>(), "first fail"));
            }

            return Task.FromResult(new ToolExecutionResult(true, new Dictionary<string, string> { ["k"] = "v" }));
        }
    }
}
