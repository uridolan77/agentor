using Agentor.Domain;
using Agentor.Domain.Enums;
using Agentor.Domain.Governance;
using Xunit;

namespace Agentor.Domain.Tests.Governance;

public sealed class PlanResumeCursorTests
{
    private static DateTimeOffset Now => DateTimeOffset.UtcNow;

    private static PlanResumeCursor MakeCursor(int remainingCount = 1) =>
        new(
            PlanId: Guid.NewGuid(),
            BlockedAtPlanStepId: Guid.NewGuid(),
            BlockedAtSourceStepId: "step-2",
            BlockedAtToolKey: "tool.risky",
            RemainingSteps: Enumerable.Range(1, remainingCount)
                .Select(i => new PendingPlanStep(
                    Guid.NewGuid(), $"step-{i + 2}", i + 2, "tool.safe",
                    RecipeStepKind.Tool, FailureHandlingPolicy.FailFast, null, null))
                .ToList(),
            CompletedStepHistory: new List<PlanStepResumeSnapshot>
            {
                new PlanStepResumeSnapshot(Guid.NewGuid(), "step-1", AgentPlanStepStatus.Completed, true,
                    new Dictionary<string, string> { ["result"] = "ok" })
            },
            SuspendedAt: Now);

    private static AgentRun CreateRunInRequiresReview(DateTimeOffset now)
    {
        var run = AgentRun.Start(Guid.NewGuid(), "Agent", "Obj", "trace-1", now);
        var step = run.StartStep("Step", now);
        var tool = ToolCall.Start(run.Id, step.Id, "tool.risky", new Dictionary<string, string>(), now);
        step.AddToolCall(tool);
        tool.MarkRequiresReview("policy flagged", now);
        step.MarkRequiresReview(now);
        run.EnterRequiresReview("policy flagged", now);
        return run;
    }

    [Fact]
    public void HasRemainingSteps_IsTrue_WhenCursorHasSteps()
    {
        var cursor = MakeCursor(remainingCount: 2);
        Assert.True(cursor.HasRemainingSteps);
    }

    [Fact]
    public void HasRemainingSteps_IsFalse_WhenNoRemainingSteps()
    {
        var cursor = new PlanResumeCursor(
            Guid.NewGuid(), Guid.NewGuid(), "step-last", "tool.x",
            RemainingSteps: [],
            CompletedStepHistory: [],
            SuspendedAt: Now);
        Assert.False(cursor.HasRemainingSteps);
    }

    [Fact]
    public void ReviewResumeState_FromCursor_ReflectsMultiStep()
    {
        var cursor = MakeCursor(remainingCount: 3);
        var state = ReviewResumeState.FromCursor(cursor);

        Assert.True(state.IsMultiStepPlan);
        Assert.Equal(3, state.RemainingStepCount);
        Assert.Equal("step-2", state.BlockedAtSourceStepId);
        Assert.Equal("tool.risky", state.BlockedAtToolKey);
    }

    [Fact]
    public void ReviewResumeState_SingleStep_IsNotMultiStep()
    {
        var state = ReviewResumeState.SingleStep("tool.foo");
        Assert.False(state.IsMultiStepPlan);
        Assert.Equal(0, state.RemainingStepCount);
        Assert.Equal("tool.foo", state.BlockedAtToolKey);
    }

    [Fact]
    public void RecordPlanResumeCursor_AcceptedWhileRequiresReview()
    {
        var now = Now;
        var run = CreateRunInRequiresReview(now);
        var cursor = MakeCursor();

        run.RecordPlanResumeCursor(cursor, now);

        Assert.NotNull(run.ResumeCursor);
        Assert.Equal(cursor.PlanId, run.ResumeCursor!.PlanId);
        Assert.Contains(run.Trace, e => e.Kind == TraceEventKind.PlanResumeCursorRecorded);
    }

    [Fact]
    public void RecordPlanResumeCursor_ThrowsWhenRunIsNotInRequiresReview()
    {
        var now = Now;
        var run = AgentRun.Start(Guid.NewGuid(), "Agent", "Obj", "trace-2", now);
        var cursor = MakeCursor();

        Assert.Throws<InvalidOperationException>(() => run.RecordPlanResumeCursor(cursor, now));
    }

    [Fact]
    public void RecordPlanResumeCursor_ThrowsWhenCursorHasNoContinuationWork()
    {
        var now = Now;
        var run = CreateRunInRequiresReview(now);
        var emptyCursor = new PlanResumeCursor(
            Guid.NewGuid(), Guid.NewGuid(), "step-last", "tool.x",
            RemainingSteps: [],
            CompletedStepHistory: [],
            SuspendedAt: now);

        Assert.Throws<ArgumentException>(() => run.RecordPlanResumeCursor(emptyCursor, now));
    }

    [Fact]
    public void RecordPlanResumeCursor_AcceptsSkillContinuation_WhenNoRemainingPlanSteps()
    {
        var now = Now;
        var run = CreateRunInRequiresReview(now);
        var skillStep = new PendingPlanStep(
            Guid.NewGuid(),
            "s2",
            2,
            string.Empty,
            RecipeStepKind.Skill,
            FailureHandlingPolicy.FailFast,
            null,
            null,
            InvokedSkillKey: "k",
            InvokedSkillVersion: AgentRecipeVersion.Parse("1.0"));
        var sc = new SkillResumeCursor(
            skillStep,
            new SkillInnerToolCheckpoint("p1", "tool.review", 1),
            new SkillProcedureResumeState(null));
        var cursor = new PlanResumeCursor(
            Guid.NewGuid(),
            skillStep.PlanStepId,
            "s2",
            "tool.review",
            RemainingSteps: [],
            CompletedStepHistory: [],
            SuspendedAt: now,
            SkillContinuation: sc);

        run.RecordPlanResumeCursor(cursor, now);
        Assert.NotNull(run.ResumeCursor);
        Assert.NotNull(run.ResumeCursor!.SkillContinuation);
    }

    [Fact]
    public void RecordPlanResumeCursor_ThrowsForNull()
    {
        var now = Now;
        var run = CreateRunInRequiresReview(now);

        Assert.Throws<ArgumentNullException>(() => run.RecordPlanResumeCursor(null!, now));
    }

    [Fact]
    public void ClearResumeCursor_ClearsCursorAndEmitsTrace()
    {
        var now = Now;
        var run = CreateRunInRequiresReview(now);
        var cursor = MakeCursor();
        run.RecordPlanResumeCursor(cursor, now);

        run.ClearResumeCursor(now);

        Assert.Null(run.ResumeCursor);
        Assert.Contains(run.Trace, e => e.Kind == TraceEventKind.PlanResumeCursorCleared);
    }

    [Fact]
    public void ClearResumeCursor_IsIdempotentWhenNoCursorPresent()
    {
        var now = Now;
        var run = CreateRunInRequiresReview(now);

        // No cursor set — should not throw
        run.ClearResumeCursor(now);

        Assert.Null(run.ResumeCursor);
        Assert.DoesNotContain(run.Trace, e => e.Kind == TraceEventKind.PlanResumeCursorCleared);
    }

    [Fact]
    public void Reconstitute_PreservesCursor()
    {
        var now = Now;
        var cursor = MakeCursor();

        var run = AgentRun.Reconstitute(
            Guid.NewGuid(), Guid.NewGuid(), "Agent", "Obj", "trace-r",
            AgentRunStatus.RequiresReview, now, now, "review",
            [], [], null, null, null, null, null, null, cursor);

        Assert.Equal(cursor, run.ResumeCursor);
    }

    [Fact]
    public void Reconstitute_WithNullCursor_HasNoCursor()
    {
        var now = Now;

        var run = AgentRun.Reconstitute(
            Guid.NewGuid(), Guid.NewGuid(), "Agent", "Obj", "trace-r2",
            AgentRunStatus.RequiresReview, now, now, "review",
            [], [], null, null, null, null, null, null, null);

        Assert.Null(run.ResumeCursor);
    }

    [Fact]
    public void CannotResumeCursorOnCompletedRun()
    {
        var now = Now;
        var run = AgentRun.Reconstitute(
            Guid.NewGuid(), Guid.NewGuid(), "Agent", "Obj", "trace-3",
            AgentRunStatus.Completed, now, now, null,
            Array.Empty<AgentStep>(), Array.Empty<ExecutionTraceEvent>());
        var cursor = MakeCursor();

        Assert.Throws<InvalidOperationException>(() => run.RecordPlanResumeCursor(cursor, now));
    }

    [Fact]
    public void CannotResumeCursorOnFailedRun()
    {
        var now = Now;
        var run = AgentRun.Start(Guid.NewGuid(), "Agent", "Obj", "trace-4", now);
        run.Fail("error", now);
        var cursor = MakeCursor();

        Assert.Throws<InvalidOperationException>(() => run.RecordPlanResumeCursor(cursor, now));
    }
}
