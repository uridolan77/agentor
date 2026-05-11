using System.Text.Json;
using System.Text.Json.Serialization;
using Agentor.Application;
using Agentor.Application.Abstractions;
using Agentor.Application.Commands;
using Agentor.Application.Coordination;
using Agentor.Domain;
using Agentor.Domain.Enums;
using Agentor.Domain.Governance;
using Agentor.Infrastructure;
using MicrosoftOptions = Microsoft.Extensions.Options.Options;
using Xunit;

namespace Agentor.Application.Tests;

/// <summary>
/// PR90 Phase 18 — validates the deterministic evaluation fixtures for multi-step review resume.
/// Closes PR53-005: multi-step plan executor resume semantics.
/// </summary>
public sealed class Phase18FixtureTests
{
    private const string ReviewTool = WellKnownToolKeys.Pr1HighRiskFakeTool;
    private const string SafeTool = WellKnownToolKeys.Pr1FakeTool;

    private sealed class ReviewOnFirstCallPolicyEvaluator : IPolicyEvaluator
    {
        private readonly HashSet<string> _reviewKeys;

        public ReviewOnFirstCallPolicyEvaluator(params string[] reviewKeys)
        {
            _reviewKeys = new HashSet<string>(reviewKeys, StringComparer.OrdinalIgnoreCase);
        }

        public Task<PolicyDecision> EvaluateToolCallAsync(PolicyEvaluationRequest request, CancellationToken ct)
        {
            PolicyDecisionOutcome outcome;
            string code, reason;

            // Resume context bypasses RequiresReview (same as real RuntimePolicyEvaluator)
            if (_reviewKeys.Contains(request.ToolKey) && request.Context?.ResumeAfterApprovedHumanReview != true)
            {
                outcome = PolicyDecisionOutcome.RequiresReview;
                code = "REQUIRES_REVIEW";
                reason = $"Tool '{request.ToolKey}' requires human review.";
            }
            else
            {
                outcome = PolicyDecisionOutcome.Allow;
                code = "ALLOW";
                reason = "Allowed.";
            }

            return Task.FromResult(new PolicyDecision(
                Guid.NewGuid(), request.RunId, request.StepId, outcome, code, reason, DateTimeOffset.UtcNow));
        }
    }

    private sealed class FixtureActorAccessor : ICurrentActorAccessor
    {
        public ActorContext Current { get; } = new(
            Guid.Parse("ffffffff-ffff-4fff-8fff-ffffffffffff"),
            "test-reviewer", ActorRole.HumanOperator);
    }

    // ───────────────────────────────────────────────────────────────────────
    // Fixture file existence — registry entries must match on-disk files
    // ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void ReviewGatedMultiStepPlan_Fixture_ExistsAndIsValidJson()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "eval", "review-gated-multistep-plan.json");
        Assert.True(File.Exists(path), $"Fixture not found: {path}");

        var json = File.ReadAllText(path);
        var doc = JsonDocument.Parse(json);
        Assert.Equal("MultiStepReviewResumeEvaluation", doc.RootElement.GetProperty("kind").GetString());
        Assert.Equal(5, doc.RootElement.GetProperty("schemaVersion").GetInt32());

        var outcome = doc.RootElement.GetProperty("expectedOutcome");
        Assert.Equal("Completed", outcome.GetProperty("runStatus").GetString());
        Assert.True(outcome.GetProperty("cursorPresentAfterPlanSuspension").GetBoolean());
        Assert.True(outcome.GetProperty("cursorClearedAfterResume").GetBoolean());
    }

    [Fact]
    public void ReviewResumeAuditExport_Fixture_ExistsAndIsValidJson()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "eval", "review-resume-audit-export.json");
        Assert.True(File.Exists(path), $"Fixture not found: {path}");

        var json = File.ReadAllText(path);
        var doc = JsonDocument.Parse(json);
        Assert.Equal("ReviewResumeAuditExport", doc.RootElement.GetProperty("kind").GetString());
        Assert.Equal(5, doc.RootElement.GetProperty("schemaVersion").GetInt32());

        var sequence = doc.RootElement.GetProperty("auditSequence");
        Assert.True(sequence.GetArrayLength() > 10, "Audit export should have >10 events");

        // Verify key events appear in order
        var events = sequence.EnumerateArray()
            .Select(e => e.GetProperty("event").GetString())
            .ToList();

        var resumeIdx = events.IndexOf("MultiStepPlanResumed");
        var cursorRecordedIdx = events.IndexOf("PlanResumeCursorRecorded");
        var cursorClearedIdx = events.IndexOf("PlanResumeCursorCleared");
        var completedIdx = events.IndexOf("RunCompleted");

        Assert.True(cursorRecordedIdx >= 0, "PlanResumeCursorRecorded must appear in audit export");
        Assert.True(cursorClearedIdx >= 0, "PlanResumeCursorCleared must appear in audit export");
        Assert.True(resumeIdx >= 0, "MultiStepPlanResumed must appear in audit export");
        Assert.True(completedIdx >= 0, "RunCompleted must appear in audit export");

        Assert.True(cursorRecordedIdx < resumeIdx, "Cursor recorded before resume");
        Assert.True(cursorClearedIdx < resumeIdx, "Cursor cleared before resume event");
        Assert.True(resumeIdx < completedIdx, "MultiStepPlanResumed before RunCompleted");
    }

    // ───────────────────────────────────────────────────────────────────────
    // Live execution — PR53-005 named evidence: the fixture scenario actually runs
    // ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PR53_005_Evidence_ThreeStepPlan_ReviewAtS2_ResumedAndCompletedByApproval()
    {
        var clock = new SystemClock();
        var counting = new CountingExecutor();
        var policy = new ReviewOnFirstCallPolicyEvaluator(ReviewTool);

        var registry = new ToolRegistry();
        registry.Register(new ToolDefinition(SafeTool, "Fake", "desc", ToolRiskLevel.Low), counting);
        registry.Register(new ToolDefinition(ReviewTool, "HighRisk", "desc", ToolRiskLevel.High), counting);

        // Step 1: run the plan; it should suspend at s2
        var executor = AgentorTestComposition.CreateSequentialPlanExecutor(registry, policy, clock);
        var run = AgentRun.Start(Guid.NewGuid(), "EvalAgent", "Prove PR53-005", "trace-pr53-005", clock.UtcNow);

        var ok = AgentRecipe.TryCreate(
            Guid.NewGuid(), "pr53-005-recipe", AgentRecipeVersion.Parse("1.0"),
            CoordinationTopology.SequentialPipeline,
            [
                new RecipeStepDefinition("s1", 1, RecipeStepKind.Tool, SafeTool),
                new RecipeStepDefinition("s2", 2, RecipeStepKind.Tool, ReviewTool),
                new RecipeStepDefinition("s3", 3, RecipeStepKind.Tool, SafeTool)
            ],
            null, out var recipe, out _);
        Assert.True(ok);

        var plan = AgentPlan.Instantiate(recipe!, Guid.NewGuid(), clock.UtcNow);
        await executor.ExecuteAsync(run, plan, CancellationToken.None);

        // Verify suspension state
        Assert.Equal(AgentRunStatus.RequiresReview, run.Status);
        Assert.NotNull(run.ResumeCursor);
        Assert.Single(run.ResumeCursor!.RemainingSteps);
        Assert.Equal("s3", run.ResumeCursor.RemainingSteps[0].SourceStepId);
        Assert.Equal("s2", run.ResumeCursor.BlockedAtSourceStepId);
        Assert.Equal(1, counting.Invocations); // only s1 executed

        Assert.Contains(run.Trace, e => e.Kind == TraceEventKind.PlanResumeCursorRecorded);
        Assert.Contains(run.Trace, e => e.Kind == TraceEventKind.PlanExecutionRequiresReview);

        // Step 2: approve — should execute s2 then s3, completing the run
        var repo = new InMemoryAgentRunRepository();
        await repo.SaveAsync(run, CancellationToken.None);

        var pipeline = new ToolExecutionPipeline(clock, MicrosoftOptions.Create(new ToolExecutionOptions()));
        var handler = new ApplyHumanReviewDecisionHandler(
            repo, policy, registry, pipeline, new FixtureActorAccessor(), clock);

        var result = await handler.HandleAsync(
            new ApplyHumanReviewDecisionCommand(run.Id, ReviewDecisionKind.Approve, null),
            CancellationToken.None);

        // Verify completion
        Assert.NotNull(result);
        Assert.Equal(AgentRunStatus.Completed, result!.Status);
        Assert.Null(result.ResumeCursor); // cursor was cleared
        Assert.Equal(3, counting.Invocations); // s1 + s2 + s3 all executed

        // Verify required trace events from audit-export fixture are present
        var traceKinds = result.Trace.Select(e => e.Kind).ToHashSet();
        Assert.Contains(TraceEventKind.PlanResumeCursorRecorded, traceKinds);
        Assert.Contains(TraceEventKind.HumanReviewDecisionRecorded, traceKinds);
        Assert.Contains(TraceEventKind.RunResumedAfterHumanReview, traceKinds);
        Assert.Contains(TraceEventKind.PlanResumeCursorCleared, traceKinds);
        Assert.Contains(TraceEventKind.MultiStepPlanResumed, traceKinds);
        Assert.Contains(TraceEventKind.PlanExecutionCompleted, traceKinds);
        Assert.Contains(TraceEventKind.RunCompleted, traceKinds);

        // Cursor cleared before MultiStepPlanResumed (ordering check)
        var clearedIndex = result.Trace.ToList().FindIndex(e => e.Kind == TraceEventKind.PlanResumeCursorCleared);
        var resumedIndex = result.Trace.ToList().FindIndex(e => e.Kind == TraceEventKind.MultiStepPlanResumed);
        Assert.True(clearedIndex < resumedIndex, "Cursor must be cleared before multi-step resume begins");
    }

    private sealed class CountingExecutor : IToolExecutor
    {
        public int Invocations;

        public Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest req, CancellationToken ct)
        {
            Invocations++;
            return Task.FromResult(new ToolExecutionResult(true,
                ToolPayload.FromLegacyDictionary(new Dictionary<string, string> { ["result"] = $"ok-{req.ToolKey}" })));
        }
    }
}
