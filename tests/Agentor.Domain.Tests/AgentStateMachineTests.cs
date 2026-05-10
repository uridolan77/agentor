using Agentor.Domain;
using Agentor.Domain.Enums;
using Xunit;

namespace Agentor.Domain.Tests;

public sealed class AgentStateMachineTests
{
    [Fact]
    public void SkippedPlanStep_CannotExecute()
    {
        var step = new AgentPlanStep(
            Guid.NewGuid(),
            "s",
            1,
            RecipeStepKind.Tool,
            "t",
            null,
            null,
            null,
            FailureHandlingPolicy.FailFast,
            null);
        step.Status = AgentPlanStepStatus.Skipped;

        Assert.Throws<InvalidOperationException>(() => AgentStateMachine.EnsurePlanStepExecutable(step));
    }

    [Fact]
    public void DeniedToolCall_CannotSucceed()
    {
        var tc = ToolCall.Reconstitute(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "t",
            ToolCallStatus.Denied,
            new Dictionary<string, string>(),
            new Dictionary<string, string>(),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            "denied");

        Assert.Throws<InvalidOperationException>(() => tc.Succeed(new Dictionary<string, string>(), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void RequiresReviewToolCall_CannotDeny()
    {
        var tc = ToolCall.Reconstitute(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "t",
            ToolCallStatus.RequiresReview,
            new Dictionary<string, string>(),
            new Dictionary<string, string>(),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            "review");

        Assert.Throws<InvalidOperationException>(() => tc.Deny("x", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void CompletedRun_CannotFail()
    {
        var run = AgentRun.Start(Guid.NewGuid(), "a", "o", "t", DateTimeOffset.UtcNow);
        var st = run.StartStep("s", DateTimeOffset.UtcNow);
        st.Complete(DateTimeOffset.UtcNow);
        run.Complete(DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => run.Fail("nope", DateTimeOffset.UtcNow));
    }
}
