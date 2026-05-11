using Agentor.Application.Abstractions;
using Agentor.Application.Commands;
using Agentor.Application.HumanReview;
using Agentor.Domain;
using Agentor.Domain.Enums;
using Agentor.Domain.Governance;
using Agentor.Infrastructure;
using Xunit;

namespace Agentor.Application.Tests;

/// <summary>
/// Focused coverage for extracted human-review services (not duplicating full MultiStepReviewResumeTests).
/// </summary>
public sealed class HumanReviewExtractedServicesTests
{
    private sealed class FixedActorAccessor : ICurrentActorAccessor
    {
        public ActorContext Current { get; } = new(Guid.NewGuid(), "op", ActorRole.HumanOperator);
    }

    private sealed class AllowAllPolicyEvaluator : IPolicyEvaluator
    {
        public Task<PolicyDecision> EvaluateToolCallAsync(PolicyEvaluationRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new PolicyDecision(
                Guid.NewGuid(),
                request.RunId,
                request.StepId,
                PolicyDecisionOutcome.Allow,
                "ALLOW",
                "Allowed.",
                DateTimeOffset.UtcNow));
    }

    private sealed class ReviewGatePolicyEvaluator : IPolicyEvaluator
    {
        private static readonly HashSet<string> ReviewKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            WellKnownToolKeys.Pr1HighRiskFakeTool
        };

        public Task<PolicyDecision> EvaluateToolCallAsync(PolicyEvaluationRequest request, CancellationToken cancellationToken)
        {
            if (ReviewKeys.Contains(request.ToolKey) && request.Context?.ResumeAfterApprovedHumanReview != true)
            {
                return Task.FromResult(new PolicyDecision(
                    Guid.NewGuid(),
                    request.RunId,
                    request.StepId,
                    PolicyDecisionOutcome.RequiresReview,
                    "REVIEW",
                    "Needs review.",
                    DateTimeOffset.UtcNow));
            }

            return Task.FromResult(new PolicyDecision(
                Guid.NewGuid(),
                request.RunId,
                request.StepId,
                PolicyDecisionOutcome.Allow,
                "ALLOW",
                "Allowed.",
                DateTimeOffset.UtcNow));
        }
    }

    [Fact]
    public async Task ReviewedToolContinuation_SingleApprovedTool_NoPlanCursor_CompletesRun()
    {
        var clock = new SystemClock();
        var now = clock.UtcNow;
        var run = AgentRun.Start(Guid.NewGuid(), "A", "O", "t-cont", now);
        var step = run.StartStep("S", now);
        var tool = ToolCall.Start(run.Id, step.Id, WellKnownToolKeys.Pr1FakeTool, new Dictionary<string, string>(), now);
        step.AddToolCall(tool);
        tool.MarkRequiresReview("needs", now);
        step.MarkRequiresReview(now);
        run.EnterRequiresReview("needs", now);

        var registry = new ToolRegistry();
        registry.Register(new ToolDefinition(WellKnownToolKeys.Pr1FakeTool, "f", "d", ToolRiskLevel.Low), new FakeToolExecutor());
        var policy = new AllowAllPolicyEvaluator();
        var pipeline = new ToolExecutionPipeline(clock, Microsoft.Extensions.Options.Options.Create(new ToolExecutionOptions()));
        var traceWriter = new ReviewTraceWriter(clock);
        var policyReeval = new ReviewPolicyReevaluationService(policy);
        var planResume = new PlanResumeOrchestrator(registry, policyReeval, pipeline, clock, traceWriter);
        var continuation = new ReviewedToolContinuationService(registry, policyReeval, pipeline, clock, traceWriter, planResume);
        var applicator = new HumanReviewDecisionApplicator(clock, new FixedActorAccessor());

        applicator.Apply(run, new ApplyHumanReviewDecisionCommand(run.Id, ReviewDecisionKind.Approve, null));
        Assert.Equal(AgentRunStatus.Running, run.Status);

        await continuation.ContinueApprovedToolExecutionAsync(run, CancellationToken.None);

        Assert.Equal(AgentRunStatus.Completed, run.Status);
        Assert.Null(run.ResumeCursor);
    }

    [Fact]
    public async Task PlanResumeOrchestrator_SingleRemainingTool_Allow_CompletesRun()
    {
        var clock = new SystemClock();
        var now = clock.UtcNow;
        var run = AgentRun.Start(Guid.NewGuid(), "A", "O", "t-plan", now);
        var pending = new PendingPlanStep(
            Guid.NewGuid(),
            "s3",
            3,
            WellKnownToolKeys.Pr1FakeTool,
            RecipeStepKind.Tool,
            FailureHandlingPolicy.FailFast,
            null,
            null);
        var cursor = new PlanResumeCursor(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "s2",
            WellKnownToolKeys.Pr1HighRiskFakeTool,
            [pending],
            [
                new PlanStepResumeSnapshot(
                    Guid.NewGuid(),
                    "s1",
                    AgentPlanStepStatus.Completed,
                    true,
                    new Dictionary<string, string> { ["r"] = "1" })
            ],
            now);

        var registry = new ToolRegistry();
        registry.Register(new ToolDefinition(WellKnownToolKeys.Pr1FakeTool, "f", "d", ToolRiskLevel.Low), new FakeToolExecutor());
        var policy = new AllowAllPolicyEvaluator();
        var pipeline = new ToolExecutionPipeline(clock, Microsoft.Extensions.Options.Options.Create(new ToolExecutionOptions()));
        var traceWriter = new ReviewTraceWriter(clock);
        var policyReeval = new ReviewPolicyReevaluationService(policy);
        var orchestrator = new PlanResumeOrchestrator(registry, policyReeval, pipeline, clock, traceWriter);

        await orchestrator.ResumeRemainingPlanStepsAsync(
            run,
            cursor,
            ToolPayload.FromLegacyDictionary(new Dictionary<string, string> { ["blocked"] = "ok" }),
            CancellationToken.None);

        Assert.Equal(AgentRunStatus.Completed, run.Status);
        Assert.Contains(run.Trace, e => e.Kind == TraceEventKind.PlanExecutionCompleted);
    }

    [Fact]
    public async Task PlanResumeOrchestrator_WhenResumedStepRequiresReview_RecordsNewCursorForLaterSteps()
    {
        var clock = new SystemClock();
        var now = clock.UtcNow;
        var run = AgentRun.Start(Guid.NewGuid(), "A", "O", "t-plan2", now);

        var reviewPending = new PendingPlanStep(
            Guid.NewGuid(),
            "s3",
            3,
            WellKnownToolKeys.Pr1HighRiskFakeTool,
            RecipeStepKind.Tool,
            FailureHandlingPolicy.FailFast,
            null,
            null);
        var tailPending = new PendingPlanStep(
            Guid.NewGuid(),
            "s4",
            4,
            WellKnownToolKeys.Pr1FakeTool,
            RecipeStepKind.Tool,
            FailureHandlingPolicy.FailFast,
            null,
            null);
        var cursor = new PlanResumeCursor(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "s2",
            WellKnownToolKeys.Pr1HighRiskFakeTool,
            [reviewPending, tailPending],
            [
                new PlanStepResumeSnapshot(
                    Guid.NewGuid(),
                    "s1",
                    AgentPlanStepStatus.Completed,
                    true,
                    new Dictionary<string, string> { ["r"] = "1" })
            ],
            now);

        var registry = new ToolRegistry();
        registry.Register(new ToolDefinition(WellKnownToolKeys.Pr1FakeTool, "f", "d", ToolRiskLevel.Low), new FakeToolExecutor());
        registry.Register(new ToolDefinition(WellKnownToolKeys.Pr1HighRiskFakeTool, "h", "d", ToolRiskLevel.High), new FakeToolExecutor());
        var policy = new ReviewGatePolicyEvaluator();
        var pipeline = new ToolExecutionPipeline(clock, Microsoft.Extensions.Options.Options.Create(new ToolExecutionOptions()));
        var traceWriter = new ReviewTraceWriter(clock);
        var policyReeval = new ReviewPolicyReevaluationService(policy);
        var orchestrator = new PlanResumeOrchestrator(registry, policyReeval, pipeline, clock, traceWriter);

        await orchestrator.ResumeRemainingPlanStepsAsync(
            run,
            cursor,
            ToolPayload.FromLegacyDictionary(new Dictionary<string, string> { ["blocked"] = "ok" }),
            CancellationToken.None);

        Assert.Equal(AgentRunStatus.RequiresReview, run.Status);
        Assert.NotNull(run.ResumeCursor);
        Assert.Single(run.ResumeCursor!.RemainingSteps);
        Assert.Equal("s4", run.ResumeCursor.RemainingSteps[0].SourceStepId);
    }

    [Fact]
    public void ReviewTraceWriter_PostReviewPolicyEvaluated_UsesScalarDataValues()
    {
        var clock = new SystemClock();
        var run = AgentRun.Start(Guid.NewGuid(), "A", "O", "t-trace", clock.UtcNow);
        var writer = new ReviewTraceWriter(clock);
        var stepId = Guid.NewGuid();
        var pd = new PolicyDecision(
            Guid.NewGuid(),
            run.Id,
            stepId,
            PolicyDecisionOutcome.Allow,
            "ALLOW",
            "Allowed after governance.",
            clock.UtcNow);

        writer.RecordPostReviewPolicyEvaluated(run, stepId, pd);

        var ev = run.Trace[^1];
        Assert.NotNull(ev.Data);
        foreach (var (_, v) in ev.Data!)
        {
            Assert.False(v.Contains('{', StringComparison.Ordinal), "Trace data values must stay scalar (no JSON payloads).");
        }
    }
}
