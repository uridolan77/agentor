using Agentor.Application;
using Agentor.Application.Abstractions;
using Agentor.Application.Commands;
using Agentor.Application.Coordination;
using Agentor.Domain;
using Agentor.Domain.Enums;
using Agentor.Domain.Governance;
using Agentor.Infrastructure;
using Agentor.Infrastructure.Conexus;
using Agentor.Infrastructure.ExternalAgents;
using Agentor.Infrastructure.Mcp;
using MicrosoftOptions = Microsoft.Extensions.Options.Options;
using Xunit;

namespace Agentor.Application.Tests;

/// <summary>
/// PR88 / Phase 18 — multi-step plan executor resume semantics.
/// Covers: cursor recording on RequiresReview, multi-step approval continuation,
/// failure policies during resumed execution, and RequiresReview chaining.
/// </summary>
public sealed class MultiStepReviewResumeTests
{
    private const string FakeTool = WellKnownToolKeys.Pr1FakeTool;
    private const string ReviewTool = WellKnownToolKeys.Pr1HighRiskFakeTool;

    private static readonly Guid ActorId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

    // ───────────────────────────────────────────────────────────────────────
    // Shared helpers
    // ───────────────────────────────────────────────────────────────────────

    private sealed class CountingExecutor : IToolExecutor
    {
        public int Invocations;
        public Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest req, CancellationToken ct)
        {
            Invocations++;
            return Task.FromResult(new ToolExecutionResult(true, new Dictionary<string, string> { ["result"] = "ok" }));
        }
    }

    private sealed class FixedActorAccessor : ICurrentActorAccessor
    {
        public ActorContext Current { get; } = new(ActorId, "operator", ActorRole.HumanOperator);
    }

    private sealed class StubPolicyEvaluator : IPolicyEvaluator
    {
        private readonly HashSet<string> _reviewKeys;
        private readonly HashSet<string> _denyKeys;

        public StubPolicyEvaluator(IEnumerable<string>? reviewKeys = null, IEnumerable<string>? denyKeys = null)
        {
            _reviewKeys = reviewKeys is not null
                ? new HashSet<string>(reviewKeys, StringComparer.OrdinalIgnoreCase)
                : [];
            _denyKeys = denyKeys is not null
                ? new HashSet<string>(denyKeys, StringComparer.OrdinalIgnoreCase)
                : [];
        }

        public Task<PolicyDecision> EvaluateToolCallAsync(PolicyEvaluationRequest req, CancellationToken ct)
        {
            PolicyDecisionOutcome outcome;
            string code, reason;

            if (_denyKeys.Contains(req.ToolKey))
            {
                outcome = PolicyDecisionOutcome.Deny;
                code = "DENY";
                reason = $"Tool '{req.ToolKey}' denied.";
            }
            else if (_reviewKeys.Contains(req.ToolKey) && req.Context?.ResumeAfterApprovedHumanReview != true)
            {
                outcome = PolicyDecisionOutcome.RequiresReview;
                code = "REQUIRES_REVIEW";
                reason = $"Tool '{req.ToolKey}' requires review.";
            }
            else
            {
                outcome = PolicyDecisionOutcome.Allow;
                code = "ALLOW";
                reason = "Allowed.";
            }

            return Task.FromResult(new PolicyDecision(
                Guid.NewGuid(), req.RunId, req.StepId, outcome, code, reason, DateTimeOffset.UtcNow));
        }
    }

    private static ToolRegistry MakeRegistry(IToolExecutor? overrideExecutor = null)
    {
        var executor = overrideExecutor ?? new FakeToolExecutor();
        var registry = new ToolRegistry();
        registry.Register(new ToolDefinition(FakeTool, "Fake", "desc", ToolRiskLevel.Low), executor);
        registry.Register(new ToolDefinition(ReviewTool, "ReviewRequired", "desc", ToolRiskLevel.High), executor);
        return registry;
    }

    private static AgentRecipe ThreeStepRecipe(
        string step1Tool = FakeTool,
        string step2Tool = ReviewTool,
        string step3Tool = FakeTool,
        FailureHandlingPolicy s3Failure = FailureHandlingPolicy.FailFast)
    {
        var ok = AgentRecipe.TryCreate(
            Guid.NewGuid(), "three-step", AgentRecipeVersion.Parse("1.0"),
            CoordinationTopology.SequentialPipeline,
            [
                new RecipeStepDefinition("s1", 1, RecipeStepKind.Tool, step1Tool),
                new RecipeStepDefinition("s2", 2, RecipeStepKind.Tool, step2Tool),
                new RecipeStepDefinition("s3", 3, RecipeStepKind.Tool, step3Tool, OnFailure: s3Failure)
            ],
            null, out var recipe, out _);
        Assert.True(ok);
        return recipe!;
    }

    private static AgentRecipe TwoStepRecipe(string step1Tool = FakeTool, string step2Tool = ReviewTool)
    {
        var ok = AgentRecipe.TryCreate(
            Guid.NewGuid(), "two-step", AgentRecipeVersion.Parse("1.0"),
            CoordinationTopology.SequentialPipeline,
            [
                new RecipeStepDefinition("s1", 1, RecipeStepKind.Tool, step1Tool),
                new RecipeStepDefinition("s2", 2, RecipeStepKind.Tool, step2Tool)
            ],
            null, out var recipe, out _);
        Assert.True(ok);
        return recipe!;
    }

    private ApplyHumanReviewDecisionHandler MakeHandler(
        IAgentRunRepository repo,
        IPolicyEvaluator policy,
        IToolExecutor? toolExecutor = null)
    {
        var registry = MakeRegistry(toolExecutor);
        var clock = new SystemClock();
        var pipeline = new ToolExecutionPipeline(clock, MicrosoftOptions.Create(new ToolExecutionOptions()));
        return new ApplyHumanReviewDecisionHandler(repo, policy, registry, pipeline, new FixedActorAccessor(), clock);
    }

    // ───────────────────────────────────────────────────────────────────────
    // PR88-a: SequentialAgentPlanExecutor records cursor for mid-plan review
    // ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Executor_RecordsCursor_WhenMidPlanStepRequiresReview()
    {
        var clock = new SystemClock();
        var counting = new CountingExecutor();
        var registry = MakeRegistry(counting);
        var policy = new StubPolicyEvaluator(reviewKeys: [ReviewTool]);
        var executor = AgentorTestComposition.CreateSequentialPlanExecutor(registry, policy, clock);

        var run = AgentRun.Start(Guid.NewGuid(), "Agent", "Obj", "trace-ms-1", clock.UtcNow);
        var recipe = ThreeStepRecipe(); // s1=FakeTool, s2=ReviewTool(review), s3=FakeTool
        var plan = AgentPlan.Instantiate(recipe, Guid.NewGuid(), clock.UtcNow);

        var result = await executor.ExecuteAsync(run, plan, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(AgentRunStatus.RequiresReview, run.Status);

        // Cursor should be recorded because s3 is remaining
        Assert.NotNull(run.ResumeCursor);
        Assert.Single(run.ResumeCursor!.RemainingSteps);
        Assert.Equal("s3", run.ResumeCursor.RemainingSteps[0].SourceStepId);
        Assert.Equal(FakeTool, run.ResumeCursor.RemainingSteps[0].ToolKey);
        Assert.Equal("s2", run.ResumeCursor.BlockedAtSourceStepId);
        Assert.Equal(ReviewTool, run.ResumeCursor.BlockedAtToolKey);

        // s1 should have completed (one invocation before review)
        Assert.Equal(1, counting.Invocations);

        // History should contain s1's completed snapshot
        Assert.Single(run.ResumeCursor.CompletedStepHistory);
        Assert.Equal("s1", run.ResumeCursor.CompletedStepHistory[0].SourceStepId);
        Assert.True(run.ResumeCursor.CompletedStepHistory[0].ToolSucceeded);

        // Trace should include cursor recorded event
        Assert.Contains(run.Trace, e => e.Kind == TraceEventKind.PlanResumeCursorRecorded);
    }

    [Fact]
    public async Task Executor_DoesNotRecordCursor_WhenLastStepRequiresReview()
    {
        var clock = new SystemClock();
        var registry = MakeRegistry();
        var policy = new StubPolicyEvaluator(reviewKeys: [ReviewTool]);
        var executor = AgentorTestComposition.CreateSequentialPlanExecutor(registry, policy, clock);

        var run = AgentRun.Start(Guid.NewGuid(), "Agent", "Obj", "trace-ms-2", clock.UtcNow);
        var recipe = TwoStepRecipe(); // s1=FakeTool, s2=ReviewTool(review) — last step
        var plan = AgentPlan.Instantiate(recipe, Guid.NewGuid(), clock.UtcNow);

        await executor.ExecuteAsync(run, plan, CancellationToken.None);

        Assert.Equal(AgentRunStatus.RequiresReview, run.Status);
        // No cursor — no remaining steps after s2
        Assert.Null(run.ResumeCursor);
        Assert.DoesNotContain(run.Trace, e => e.Kind == TraceEventKind.PlanResumeCursorRecorded);
    }

    // ───────────────────────────────────────────────────────────────────────
    // PR88-b: Approval resumes multi-step plan (the core invariant)
    // ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Approve_WithCursor_ResumesRemainingStepsAndCompletesRun()
    {
        var clock = new SystemClock();
        var counting = new CountingExecutor();
        var policy = new StubPolicyEvaluator(reviewKeys: [ReviewTool]);
        var repo = new InMemoryAgentRunRepository();

        // Run plan to RequiresReview state
        var registry = MakeRegistry(counting);
        var executor = AgentorTestComposition.CreateSequentialPlanExecutor(registry, policy, clock);
        var run = AgentRun.Start(Guid.NewGuid(), "Agent", "Obj", "trace-ms-3", clock.UtcNow);
        var plan = AgentPlan.Instantiate(ThreeStepRecipe(), Guid.NewGuid(), clock.UtcNow);
        await executor.ExecuteAsync(run, plan, CancellationToken.None);

        Assert.Equal(AgentRunStatus.RequiresReview, run.Status);
        Assert.NotNull(run.ResumeCursor);
        var invocationsBeforeApproval = counting.Invocations; // should be 1 (s1 completed)

        await repo.SaveAsync(run, CancellationToken.None);

        // Now approve — handler should execute the blocked s2 tool AND resume s3
        var handler = MakeHandler(repo, policy, counting);
        var result = await handler.HandleAsync(
            new ApplyHumanReviewDecisionCommand(run.Id, ReviewDecisionKind.Approve, null),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(AgentRunStatus.Completed, result!.Status);
        Assert.Null(result.ResumeCursor); // cursor cleared after resume

        // Total invocations: s1=1, s2 (approved)=1, s3=1
        Assert.Equal(invocationsBeforeApproval + 2, counting.Invocations);

        // Trace must contain multi-step resume event and plan completion
        Assert.Contains(result.Trace, e => e.Kind == TraceEventKind.MultiStepPlanResumed);
        Assert.Contains(result.Trace, e => e.Kind == TraceEventKind.PlanResumeCursorCleared);
        Assert.Contains(result.Trace, e => e.Kind == TraceEventKind.PlanExecutionCompleted);
    }

    [Fact]
    public async Task Approve_WithoutCursor_CompletesSingleStepRun()
    {
        var clock = new SystemClock();
        var counting = new CountingExecutor();
        var policy = new StubPolicyEvaluator(reviewKeys: [ReviewTool]);
        var repo = new InMemoryAgentRunRepository();

        // Two-step plan — s2 is the last step, no cursor expected
        var registry = MakeRegistry(counting);
        var executor = AgentorTestComposition.CreateSequentialPlanExecutor(registry, policy, clock);
        var run = AgentRun.Start(Guid.NewGuid(), "Agent", "Obj", "trace-ms-4", clock.UtcNow);
        var plan = AgentPlan.Instantiate(TwoStepRecipe(), Guid.NewGuid(), clock.UtcNow);
        await executor.ExecuteAsync(run, plan, CancellationToken.None);

        Assert.Equal(AgentRunStatus.RequiresReview, run.Status);
        Assert.Null(run.ResumeCursor);
        await repo.SaveAsync(run, CancellationToken.None);

        var handler = MakeHandler(repo, policy, counting);
        var result = await handler.HandleAsync(
            new ApplyHumanReviewDecisionCommand(run.Id, ReviewDecisionKind.Approve, null),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(AgentRunStatus.Completed, result!.Status);
        Assert.DoesNotContain(result.Trace, e => e.Kind == TraceEventKind.MultiStepPlanResumed);
    }

    // ───────────────────────────────────────────────────────────────────────
    // PR88-c: Reject fails the run without executing remaining steps
    // ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reject_WithCursor_FailsRunWithoutExecutingRemainingSteps()
    {
        var clock = new SystemClock();
        var counting = new CountingExecutor();
        var policy = new StubPolicyEvaluator(reviewKeys: [ReviewTool]);
        var repo = new InMemoryAgentRunRepository();

        var registry = MakeRegistry(counting);
        var executor = AgentorTestComposition.CreateSequentialPlanExecutor(registry, policy, clock);
        var run = AgentRun.Start(Guid.NewGuid(), "Agent", "Obj", "trace-ms-5", clock.UtcNow);
        var plan = AgentPlan.Instantiate(ThreeStepRecipe(), Guid.NewGuid(), clock.UtcNow);
        await executor.ExecuteAsync(run, plan, CancellationToken.None);

        var invocationsBeforeDecision = counting.Invocations;
        await repo.SaveAsync(run, CancellationToken.None);

        var handler = MakeHandler(repo, policy, counting);
        var result = await handler.HandleAsync(
            new ApplyHumanReviewDecisionCommand(run.Id, ReviewDecisionKind.Reject, "Rejected by operator."),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(AgentRunStatus.Failed, result!.Status);
        // No additional tool executions after reject
        Assert.Equal(invocationsBeforeDecision, counting.Invocations);
        Assert.DoesNotContain(result.Trace, e => e.Kind == TraceEventKind.MultiStepPlanResumed);
    }

    // ───────────────────────────────────────────────────────────────────────
    // PR88-d: Failure policies on resumed steps
    // ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Approve_DeniedResumedStep_WithContinueOnFailure_CompletesRun()
    {
        var clock = new SystemClock();
        var counting = new CountingExecutor();
        // Step 3 tool will be denied during resume
        var deniedResumedTool = "tool.denied-on-resume";
        var policy = new StubPolicyEvaluator(
            reviewKeys: [ReviewTool],
            denyKeys: [deniedResumedTool]);
        var repo = new InMemoryAgentRunRepository();

        var registry = MakeRegistry(counting);
        registry.Register(new ToolDefinition(deniedResumedTool, "Denied", "desc", ToolRiskLevel.Low), counting);

        var executor = AgentorTestComposition.CreateSequentialPlanExecutor(registry, policy, clock);
        var run = AgentRun.Start(Guid.NewGuid(), "Agent", "Obj", "trace-ms-6", clock.UtcNow);

        // 3-step: s1=FakeTool, s2=ReviewTool(review), s3=deniedResumedTool(ContinueOnFailure)
        var ok = AgentRecipe.TryCreate(
            Guid.NewGuid(), "three-step-cont", AgentRecipeVersion.Parse("1.0"),
            CoordinationTopology.SequentialPipeline,
            [
                new RecipeStepDefinition("s1", 1, RecipeStepKind.Tool, FakeTool),
                new RecipeStepDefinition("s2", 2, RecipeStepKind.Tool, ReviewTool),
                new RecipeStepDefinition("s3", 3, RecipeStepKind.Tool, deniedResumedTool,
                    OnFailure: FailureHandlingPolicy.ContinueOnFailure)
            ],
            null, out var recipe, out _);
        Assert.True(ok);
        var plan = AgentPlan.Instantiate(recipe!, Guid.NewGuid(), clock.UtcNow);
        await executor.ExecuteAsync(run, plan, CancellationToken.None);

        await repo.SaveAsync(run, CancellationToken.None);
        var handler = new ApplyHumanReviewDecisionHandler(
            repo, policy, registry,
            new ToolExecutionPipeline(clock, MicrosoftOptions.Create(new ToolExecutionOptions())),
            new FixedActorAccessor(), clock);

        var result = await handler.HandleAsync(
            new ApplyHumanReviewDecisionCommand(run.Id, ReviewDecisionKind.Approve, null),
            CancellationToken.None);

        // ContinueOnFailure means run completes even though s3 was denied
        Assert.NotNull(result);
        Assert.Equal(AgentRunStatus.Completed, result!.Status);
        Assert.Contains(result.Trace, e => e.Kind == TraceEventKind.MultiStepPlanResumed);
        Assert.Contains(result.Trace, e => e.Kind == TraceEventKind.PlanExecutionCompleted);
    }

    [Fact]
    public async Task Approve_FailedResumedStep_WithFailFast_FailsRun()
    {
        var clock = new SystemClock();
        var failExecutor = new FailingExecutor();
        var policy = new StubPolicyEvaluator(reviewKeys: [ReviewTool]);
        var repo = new InMemoryAgentRunRepository();

        var registry = MakeRegistry();
        registry.Register(new ToolDefinition("tool.will-fail", "WillFail", "desc", ToolRiskLevel.Low), failExecutor);

        var executor = AgentorTestComposition.CreateSequentialPlanExecutor(registry, policy, clock);
        var run = AgentRun.Start(Guid.NewGuid(), "Agent", "Obj", "trace-ms-7", clock.UtcNow);

        var ok = AgentRecipe.TryCreate(
            Guid.NewGuid(), "three-step-fail", AgentRecipeVersion.Parse("1.0"),
            CoordinationTopology.SequentialPipeline,
            [
                new RecipeStepDefinition("s1", 1, RecipeStepKind.Tool, FakeTool),
                new RecipeStepDefinition("s2", 2, RecipeStepKind.Tool, ReviewTool),
                new RecipeStepDefinition("s3", 3, RecipeStepKind.Tool, "tool.will-fail",
                    OnFailure: FailureHandlingPolicy.FailFast)
            ],
            null, out var recipe, out _);
        Assert.True(ok);
        var plan = AgentPlan.Instantiate(recipe!, Guid.NewGuid(), clock.UtcNow);
        await executor.ExecuteAsync(run, plan, CancellationToken.None);

        await repo.SaveAsync(run, CancellationToken.None);
        var handler = new ApplyHumanReviewDecisionHandler(
            repo, policy, registry,
            new ToolExecutionPipeline(clock, MicrosoftOptions.Create(new ToolExecutionOptions())),
            new FixedActorAccessor(), clock);

        var result = await handler.HandleAsync(
            new ApplyHumanReviewDecisionCommand(run.Id, ReviewDecisionKind.Approve, null),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(AgentRunStatus.Failed, result!.Status);
        Assert.Contains(result.Trace, e => e.Kind == TraceEventKind.MultiStepPlanResumed);
        Assert.DoesNotContain(result.Trace, e => e.Kind == TraceEventKind.PlanExecutionCompleted);
    }

    [Fact]
    public async Task Approve_DeniedResumedStep_WithEscalateToReview_LeavesPendingReviewTool_AndSecondApproveFailsSafely()
    {
        var clock = new SystemClock();
        var counting = new CountingExecutor();
        var deniedResumedTool = "tool.denied-after-resume";
        var policy = new StubPolicyEvaluator(
            reviewKeys: [ReviewTool],
            denyKeys: [deniedResumedTool]);
        var repo = new InMemoryAgentRunRepository();

        var registry = MakeRegistry(counting);
        registry.Register(new ToolDefinition(deniedResumedTool, "Denied", "desc", ToolRiskLevel.Low), counting);

        var executor = AgentorTestComposition.CreateSequentialPlanExecutor(registry, policy, clock);
        var run = AgentRun.Start(Guid.NewGuid(), "Agent", "Obj", "trace-ms-7b", clock.UtcNow);

        var ok = AgentRecipe.TryCreate(
            Guid.NewGuid(), "three-step-escalate-deny", AgentRecipeVersion.Parse("1.0"),
            CoordinationTopology.SequentialPipeline,
            [
                new RecipeStepDefinition("s1", 1, RecipeStepKind.Tool, FakeTool),
                new RecipeStepDefinition("s2", 2, RecipeStepKind.Tool, ReviewTool),
                new RecipeStepDefinition("s3", 3, RecipeStepKind.Tool, deniedResumedTool,
                    OnFailure: FailureHandlingPolicy.EscalateToReview),
                new RecipeStepDefinition("s4", 4, RecipeStepKind.Tool, FakeTool)
            ],
            null, out var recipe, out _);
        Assert.True(ok);
        var plan = AgentPlan.Instantiate(recipe!, Guid.NewGuid(), clock.UtcNow);
        await executor.ExecuteAsync(run, plan, CancellationToken.None);

        await repo.SaveAsync(run, CancellationToken.None);
        var handler = new ApplyHumanReviewDecisionHandler(
            repo, policy, registry,
            new ToolExecutionPipeline(clock, MicrosoftOptions.Create(new ToolExecutionOptions { MaxAttempts = 1 })),
            new FixedActorAccessor(), clock);

        var firstApproval = await handler.HandleAsync(
            new ApplyHumanReviewDecisionCommand(run.Id, ReviewDecisionKind.Approve, null),
            CancellationToken.None);

        Assert.NotNull(firstApproval);
        Assert.Equal(AgentRunStatus.RequiresReview, firstApproval!.Status);
        Assert.NotNull(firstApproval.ResumeCursor);
        Assert.Equal("s3", firstApproval.ResumeCursor!.BlockedAtSourceStepId);
        Assert.Single(firstApproval.ResumeCursor.RemainingSteps);
        Assert.Equal("s4", firstApproval.ResumeCursor.RemainingSteps[0].SourceStepId);

        var resumedReviewStep = firstApproval.Steps.Last();
        Assert.Equal(AgentStepStatus.RequiresReview, resumedReviewStep.Status);
        Assert.Equal(ToolCallStatus.RequiresReview, resumedReviewStep.ToolCalls.Last().Status);

        var secondApproval = await handler.HandleAsync(
            new ApplyHumanReviewDecisionCommand(run.Id, ReviewDecisionKind.Approve, null),
            CancellationToken.None);

        Assert.NotNull(secondApproval);
        Assert.Equal(AgentRunStatus.Failed, secondApproval!.Status);
        Assert.DoesNotContain(secondApproval.Trace, e => e.Kind == TraceEventKind.PlanExecutionCompleted);
        Assert.Equal(2, counting.Invocations);
    }

    [Fact]
    public async Task Approve_FailedResumedStep_WithEscalateToReview_LeavesPendingReviewTool_AndSecondApproveRetriesAndCompletes()
    {
        var clock = new SystemClock();
        var failOnceExecutor = new FailOnceExecutor();
        var policy = new StubPolicyEvaluator(reviewKeys: [ReviewTool]);
        var repo = new InMemoryAgentRunRepository();

        var registry = MakeRegistry();
        registry.Register(new ToolDefinition("tool.fail-once", "FailOnce", "desc", ToolRiskLevel.Low), failOnceExecutor);

        var executor = AgentorTestComposition.CreateSequentialPlanExecutor(registry, policy, clock);
        var run = AgentRun.Start(Guid.NewGuid(), "Agent", "Obj", "trace-ms-7c", clock.UtcNow);

        var ok = AgentRecipe.TryCreate(
            Guid.NewGuid(), "three-step-escalate-failure", AgentRecipeVersion.Parse("1.0"),
            CoordinationTopology.SequentialPipeline,
            [
                new RecipeStepDefinition("s1", 1, RecipeStepKind.Tool, FakeTool),
                new RecipeStepDefinition("s2", 2, RecipeStepKind.Tool, ReviewTool),
                new RecipeStepDefinition("s3", 3, RecipeStepKind.Tool, "tool.fail-once",
                    OnFailure: FailureHandlingPolicy.EscalateToReview),
                new RecipeStepDefinition("s4", 4, RecipeStepKind.Tool, FakeTool)
            ],
            null, out var recipe, out _);
        Assert.True(ok);
        var plan = AgentPlan.Instantiate(recipe!, Guid.NewGuid(), clock.UtcNow);
        await executor.ExecuteAsync(run, plan, CancellationToken.None);

        await repo.SaveAsync(run, CancellationToken.None);
        var handler = new ApplyHumanReviewDecisionHandler(
            repo, policy, registry,
            new ToolExecutionPipeline(clock, MicrosoftOptions.Create(new ToolExecutionOptions { MaxAttempts = 1 })),
            new FixedActorAccessor(), clock);

        var firstApproval = await handler.HandleAsync(
            new ApplyHumanReviewDecisionCommand(run.Id, ReviewDecisionKind.Approve, null),
            CancellationToken.None);

        Assert.NotNull(firstApproval);
        Assert.Equal(AgentRunStatus.RequiresReview, firstApproval!.Status);
        Assert.NotNull(firstApproval.ResumeCursor);
        Assert.Equal("s3", firstApproval.ResumeCursor!.BlockedAtSourceStepId);
        Assert.Single(firstApproval.ResumeCursor.RemainingSteps);

        var resumedReviewStep = firstApproval.Steps.Last();
        Assert.Equal(AgentStepStatus.RequiresReview, resumedReviewStep.Status);
        Assert.Equal(ToolCallStatus.RequiresReview, resumedReviewStep.ToolCalls.Last().Status);

        var secondApproval = await handler.HandleAsync(
            new ApplyHumanReviewDecisionCommand(run.Id, ReviewDecisionKind.Approve, null),
            CancellationToken.None);

        Assert.NotNull(secondApproval);
        Assert.Equal(AgentRunStatus.Completed, secondApproval!.Status);
        Assert.Null(secondApproval.ResumeCursor);
        Assert.Contains(secondApproval.Trace, e => e.Kind == TraceEventKind.PlanExecutionCompleted);
        Assert.Equal(4, secondApproval.Steps.Count);
    }

    // ───────────────────────────────────────────────────────────────────────
    // PR88-e: RequiresReview chaining (second review gate in the plan)
    // ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Approve_ResumedStepRequiresReview_RecordsNewCursorAndEntersReview()
    {
        var clock = new SystemClock();
        var counting = new CountingExecutor();
        // Both step 2 and step 3 require review (two-gate plan)
        var policy = new StubPolicyEvaluator(reviewKeys: [ReviewTool]);
        var repo = new InMemoryAgentRunRepository();

        var registry = new ToolRegistry();
        registry.Register(new ToolDefinition(FakeTool, "Fake", "desc", ToolRiskLevel.Low), counting);
        registry.Register(new ToolDefinition(ReviewTool, "Review", "desc", ToolRiskLevel.High), counting);

        var executor = AgentorTestComposition.CreateSequentialPlanExecutor(registry, policy, clock);
        var run = AgentRun.Start(Guid.NewGuid(), "Agent", "Obj", "trace-ms-8", clock.UtcNow);

        // s1=FakeTool, s2=ReviewTool(review), s3=ReviewTool(review), s4=FakeTool
        var ok = AgentRecipe.TryCreate(
            Guid.NewGuid(), "four-step-two-review", AgentRecipeVersion.Parse("1.0"),
            CoordinationTopology.SequentialPipeline,
            [
                new RecipeStepDefinition("s1", 1, RecipeStepKind.Tool, FakeTool),
                new RecipeStepDefinition("s2", 2, RecipeStepKind.Tool, ReviewTool),
                new RecipeStepDefinition("s3", 3, RecipeStepKind.Tool, ReviewTool),
                new RecipeStepDefinition("s4", 4, RecipeStepKind.Tool, FakeTool)
            ],
            null, out var recipe, out _);
        Assert.True(ok);
        var plan = AgentPlan.Instantiate(recipe!, Guid.NewGuid(), clock.UtcNow);
        await executor.ExecuteAsync(run, plan, CancellationToken.None);

        Assert.Equal(AgentRunStatus.RequiresReview, run.Status);
        Assert.NotNull(run.ResumeCursor);
        Assert.Equal(2, run.ResumeCursor!.RemainingSteps.Count); // s3 and s4 remaining

        await repo.SaveAsync(run, CancellationToken.None);

        // First approval: s2 executes (policy clears it on resume context), s3 hits review again
        var handler = new ApplyHumanReviewDecisionHandler(
            repo, policy, registry,
            new ToolExecutionPipeline(clock, MicrosoftOptions.Create(new ToolExecutionOptions())),
            new FixedActorAccessor(), clock);

        var result = await handler.HandleAsync(
            new ApplyHumanReviewDecisionCommand(run.Id, ReviewDecisionKind.Approve, null),
            CancellationToken.None);

        Assert.NotNull(result);
        // After first approval: s2 is done (runs fine because ResumeAfterApprovedHumanReview=true bypasses review),
        // s3 (ReviewTool without resume context) requires review again
        Assert.Equal(AgentRunStatus.RequiresReview, result!.Status);

        // A new cursor should be recorded for s4 (remaining after s3)
        Assert.NotNull(result.ResumeCursor);
        Assert.Equal("s3", result.ResumeCursor!.BlockedAtSourceStepId);
        Assert.Single(result.ResumeCursor.RemainingSteps);
        Assert.Equal("s4", result.ResumeCursor.RemainingSteps[0].SourceStepId);

        // Multi-step resume trace should appear
        Assert.Contains(result.Trace, e => e.Kind == TraceEventKind.MultiStepPlanResumed);
        // New cursor should be recorded
        var cursorTraces = result.Trace.Where(e => e.Kind == TraceEventKind.PlanResumeCursorRecorded).ToList();
        Assert.Equal(2, cursorTraces.Count); // first from executor, second from handler resume
    }

    // ───────────────────────────────────────────────────────────────────────
    // PR88-f: RequestChanges and Escalate record decision, do not execute
    // ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RequestChanges_WithCursor_RecordsDecisionWithoutExecutingSteps()
    {
        var clock = new SystemClock();
        var counting = new CountingExecutor();
        var policy = new StubPolicyEvaluator(reviewKeys: [ReviewTool]);
        var repo = new InMemoryAgentRunRepository();

        var registry = MakeRegistry(counting);
        var executor = AgentorTestComposition.CreateSequentialPlanExecutor(registry, policy, clock);
        var run = AgentRun.Start(Guid.NewGuid(), "Agent", "Obj", "trace-ms-9", clock.UtcNow);
        var plan = AgentPlan.Instantiate(ThreeStepRecipe(), Guid.NewGuid(), clock.UtcNow);
        await executor.ExecuteAsync(run, plan, CancellationToken.None);

        var invocationsBeforeDecision = counting.Invocations;
        await repo.SaveAsync(run, CancellationToken.None);

        var handler = MakeHandler(repo, policy, counting);
        var result = await handler.HandleAsync(
            new ApplyHumanReviewDecisionCommand(run.Id, ReviewDecisionKind.RequestChanges, "Please adjust input."),
            CancellationToken.None);

        Assert.NotNull(result);
        // RequestChanges leaves run in RequiresReview
        Assert.Equal(AgentRunStatus.RequiresReview, result!.Status);
        // No additional tool executions
        Assert.Equal(invocationsBeforeDecision, counting.Invocations);
        Assert.Contains(result.HumanReviewDecisions, d => d.Kind == ReviewDecisionKind.RequestChanges);
        Assert.DoesNotContain(result.Trace, e => e.Kind == TraceEventKind.MultiStepPlanResumed);
    }

    [Fact]
    public async Task Escalate_WithCursor_RecordsDecisionWithoutExecutingSteps()
    {
        var clock = new SystemClock();
        var counting = new CountingExecutor();
        var policy = new StubPolicyEvaluator(reviewKeys: [ReviewTool]);
        var repo = new InMemoryAgentRunRepository();

        var registry = MakeRegistry(counting);
        var executor = AgentorTestComposition.CreateSequentialPlanExecutor(registry, policy, clock);
        var run = AgentRun.Start(Guid.NewGuid(), "Agent", "Obj", "trace-ms-10", clock.UtcNow);
        var plan = AgentPlan.Instantiate(ThreeStepRecipe(), Guid.NewGuid(), clock.UtcNow);
        await executor.ExecuteAsync(run, plan, CancellationToken.None);

        var invocationsBeforeDecision = counting.Invocations;
        await repo.SaveAsync(run, CancellationToken.None);

        var handler = MakeHandler(repo, policy, counting);
        var result = await handler.HandleAsync(
            new ApplyHumanReviewDecisionCommand(run.Id, ReviewDecisionKind.Escalate, "Escalated to senior."),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(AgentRunStatus.RequiresReview, result!.Status);
        Assert.Equal(invocationsBeforeDecision, counting.Invocations);
        Assert.Contains(result.HumanReviewDecisions, d => d.Kind == ReviewDecisionKind.Escalate);
    }

    // ───────────────────────────────────────────────────────────────────────
    // Helpers
    // ───────────────────────────────────────────────────────────────────────

    private sealed class FailingExecutor : IToolExecutor
    {
        public Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest req, CancellationToken ct) =>
            Task.FromResult(new ToolExecutionResult(false, new Dictionary<string, string>(), "Simulated tool failure."));
    }

    private sealed class FailOnceExecutor : IToolExecutor
    {
        private int _attempts;

        public Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest req, CancellationToken ct)
        {
            _attempts++;
            if (_attempts == 1)
            {
                return Task.FromResult(new ToolExecutionResult(false, new Dictionary<string, string>(), "Fail once during resume."));
            }

            return Task.FromResult(new ToolExecutionResult(true, new Dictionary<string, string> { ["result"] = "recovered" }));
        }
    }
}
