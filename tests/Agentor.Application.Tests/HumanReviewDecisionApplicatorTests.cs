using Agentor.Application.Abstractions;
using Agentor.Application.Commands;
using Agentor.Application.HumanReview;
using Agentor.Domain;
using Agentor.Domain.Enums;
using Agentor.Domain.Governance;
using Agentor.Infrastructure;
using Xunit;

namespace Agentor.Application.Tests;

public sealed class HumanReviewDecisionApplicatorTests
{
    private sealed class FixedActorAccessor(Guid actorId, ActorRole role = ActorRole.HumanOperator) : ICurrentActorAccessor
    {
        public ActorContext Current { get; } = new(actorId, "test", role);
    }

    private static AgentRun CreateRunInHumanReview(DateTimeOffset now)
    {
        var run = AgentRun.Start(Guid.NewGuid(), "Agent", "Obj", "t", now);
        var step = run.StartStep("s", now);
        var tool = ToolCall.Start(run.Id, step.Id, WellKnownToolKeys.Pr1FakeTool, new Dictionary<string, string>(), now);
        step.AddToolCall(tool);
        tool.MarkRequiresReview("r", now);
        step.MarkRequiresReview(now);
        run.EnterRequiresReview("r", now);
        return run;
    }

    [Fact]
    public void Apply_Throws_WhenRunNotRequiresReview()
    {
        var clock = new SystemClock();
        var now = clock.UtcNow;
        var run = AgentRun.Start(Guid.NewGuid(), "A", "O", "t2", now);
        var applicator = new HumanReviewDecisionApplicator(clock, new FixedActorAccessor(Guid.NewGuid()));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            applicator.Apply(run, new ApplyHumanReviewDecisionCommand(run.Id, ReviewDecisionKind.Approve, null)));

        Assert.Contains("requires review", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Apply_Throws_WhenRequestChangesNoteMissing()
    {
        var clock = new SystemClock();
        var now = clock.UtcNow;
        var run = CreateRunInHumanReview(now);
        var applicator = new HumanReviewDecisionApplicator(clock, new FixedActorAccessor(Guid.NewGuid()));

        Assert.Throws<InvalidOperationException>(() =>
            applicator.Apply(run, new ApplyHumanReviewDecisionCommand(run.Id, ReviewDecisionKind.RequestChanges, "  ")));
    }

    [Fact]
    public void Apply_Throws_WhenEscalateNoteMissing()
    {
        var clock = new SystemClock();
        var now = clock.UtcNow;
        var run = CreateRunInHumanReview(now);
        var applicator = new HumanReviewDecisionApplicator(clock, new FixedActorAccessor(Guid.NewGuid()));

        Assert.Throws<InvalidOperationException>(() =>
            applicator.Apply(run, new ApplyHumanReviewDecisionCommand(run.Id, ReviewDecisionKind.Escalate, null)));
    }

    [Fact]
    public void Apply_Throws_WhenActorIdEmpty()
    {
        var clock = new SystemClock();
        var now = clock.UtcNow;
        var run = CreateRunInHumanReview(now);
        var applicator = new HumanReviewDecisionApplicator(clock, new FixedActorAccessor(Guid.Empty));

        Assert.Throws<InvalidOperationException>(() =>
            applicator.Apply(run, new ApplyHumanReviewDecisionCommand(run.Id, ReviewDecisionKind.Reject, null)));
    }

    [Fact]
    public void Apply_Throws_WhenEscalatedApprove_NotGovernanceApprover()
    {
        var clock = new SystemClock();
        var now = clock.UtcNow;
        var run = CreateRunInHumanReview(now);
        var actorId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
        var applicator = new HumanReviewDecisionApplicator(clock, new FixedActorAccessor(actorId));

        applicator.Apply(run, new ApplyHumanReviewDecisionCommand(run.Id, ReviewDecisionKind.Escalate, "up"));

        Assert.Equal(HumanReviewWorkflowStatus.Escalated, run.ReviewWorkflowStatus);

        Assert.Throws<InvalidOperationException>(() =>
            applicator.Apply(run, new ApplyHumanReviewDecisionCommand(run.Id, ReviewDecisionKind.Approve, null)));
    }
}
