using Agentor.Domain;
using Agentor.Domain.Enums;
using Agentor.Domain.Governance;

namespace Agentor.Domain.Tests;

public sealed class AgentRunTests
{
    [Fact]
    public void Start_CreatesRunningRun_WithInitialTrace()
    {
        var run = AgentRun.Start(Guid.NewGuid(), "Test Agent", "Test objective", "trace-1", DateTimeOffset.UtcNow);

        Assert.Equal(AgentRunStatus.Running, run.Status);
        Assert.NotEqual(Guid.Empty, run.Id);
        Assert.Single(run.Trace);
        Assert.Equal(TraceEventKind.RunStarted, run.Trace[0].Kind);
    }

    [Fact]
    public void Complete_RequiresAtLeastOneStep()
    {
        var run = AgentRun.Start(Guid.NewGuid(), "Test Agent", "Test objective", "trace-1", DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => run.Complete(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void StartStep_AddsRunningStep()
    {
        var run = AgentRun.Start(Guid.NewGuid(), "Test Agent", "Test objective", "trace-1", DateTimeOffset.UtcNow);

        var step = run.StartStep("Step one", DateTimeOffset.UtcNow);

        Assert.Single(run.Steps);
        Assert.Equal(AgentStepStatus.Running, step.Status);
        Assert.Equal(1, step.Index);
    }

    [Fact]
    public void Start_WithExplicitProjectId_UsesItForAthanorProjectResolution()
    {
        var profileId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var scope = new AgentRunScope(null, null, projectId, null);
        var run = AgentRun.Start(profileId, "Test Agent", "Test objective", "trace-scope", DateTimeOffset.UtcNow, scope);

        Assert.Equal(projectId, run.ResolveAthanorProjectId());
        Assert.Equal(profileId, run.ProfileId);
    }

    [Fact]
    public void Start_WithoutExplicitProjectId_UsesProfileIdForAthanorProjectResolution()
    {
        var profileId = Guid.NewGuid();
        var run = AgentRun.Start(profileId, "Test Agent", "Test objective", "trace-fallback", DateTimeOffset.UtcNow);

        Assert.Equal(profileId, run.ResolveAthanorProjectId());
        Assert.Null(run.ProjectId);
    }

    [Fact]
    public void ApplyHumanReview_Approve_ResumesRunAndToolFromRequiresReview()
    {
        var now = DateTimeOffset.UtcNow;
        var run = AgentRun.Start(Guid.NewGuid(), "Agent", "Objective", "trace-approve", now);
        var step = run.StartStep("Step", now);
        var tool = ToolCall.Start(run.Id, step.Id, "demo.tool", new Dictionary<string, string>(), now);
        step.AddToolCall(tool);
        tool.MarkRequiresReview("review", now);
        step.MarkRequiresReview(now);
        run.EnterRequiresReview("review", now);

        var decision = new HumanReviewDecision(
            Guid.NewGuid(),
            ReviewDecisionKind.Approve,
            Guid.NewGuid(),
            now,
            null,
            ReviewResolutionStatus.ResolvedApproved);

        run.ApplyHumanReviewDecision(decision, now);

        Assert.Equal(AgentRunStatus.Running, run.Status);
        Assert.Equal(AgentStepStatus.Running, step.Status);
        Assert.Equal(ToolCallStatus.Running, tool.Status);
        Assert.Single(run.HumanReviewDecisions);
    }

    [Fact]
    public void ApplyHumanReview_Reject_FailsRun()
    {
        var now = DateTimeOffset.UtcNow;
        var run = AgentRun.Start(Guid.NewGuid(), "Agent", "Objective", "trace-reject", now);
        var step = run.StartStep("Step", now);
        var tool = ToolCall.Start(run.Id, step.Id, "demo.tool", new Dictionary<string, string>(), now);
        step.AddToolCall(tool);
        tool.MarkRequiresReview("review", now);
        step.MarkRequiresReview(now);
        run.EnterRequiresReview("review", now);

        var decision = new HumanReviewDecision(
            Guid.NewGuid(),
            ReviewDecisionKind.Reject,
            Guid.NewGuid(),
            now,
            "no go",
            ReviewResolutionStatus.ResolvedRejected);

        run.ApplyHumanReviewDecision(decision, now);

        Assert.Equal(AgentRunStatus.Failed, run.Status);
        Assert.Equal(AgentStepStatus.Failed, step.Status);
        Assert.Equal(ToolCallStatus.Failed, tool.Status);
    }

    [Fact]
    public void ApplyHumanReview_RequestChanges_RecordsDecision_WithoutResumingExecution()
    {
        var now = DateTimeOffset.UtcNow;
        var run = AgentRun.Start(Guid.NewGuid(), "Agent", "Objective", "trace-changes", now);
        var step = run.StartStep("Step", now);
        var tool = ToolCall.Start(run.Id, step.Id, "demo.tool", new Dictionary<string, string>(), now);
        step.AddToolCall(tool);
        tool.MarkRequiresReview("review", now);
        step.MarkRequiresReview(now);
        run.EnterRequiresReview("review", now);

        var decision = new HumanReviewDecision(
            Guid.NewGuid(),
            ReviewDecisionKind.RequestChanges,
            Guid.NewGuid(),
            now,
            "please revise",
            ReviewResolutionStatus.ChangesRequested);

        run.ApplyHumanReviewDecision(decision, now);

        Assert.Equal(AgentRunStatus.RequiresReview, run.Status);
        Assert.Equal(AgentStepStatus.RequiresReview, step.Status);
        Assert.Equal(ToolCallStatus.RequiresReview, tool.Status);
        Assert.Single(run.HumanReviewDecisions);
    }
}
