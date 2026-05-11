using Agentor.Application.Abstractions;
using Agentor.Application.HumanReview;
using Agentor.Domain;
using Agentor.Domain.Enums;
using Agentor.Domain.Governance;
using Agentor.Infrastructure;
using Xunit;

namespace Agentor.Application.Tests;

public sealed class ReviewPolicyReevaluationServiceTests
{
    private sealed class CapturingPolicyEvaluator : IPolicyEvaluator
    {
        public PolicyEvaluationRequest? LastRequest { get; private set; }

        public Task<PolicyDecision> EvaluateToolCallAsync(PolicyEvaluationRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new PolicyDecision(
                Guid.NewGuid(),
                request.RunId,
                request.StepId,
                PolicyDecisionOutcome.Allow,
                "ALLOW",
                "ok",
                DateTimeOffset.UtcNow));
        }
    }

    [Fact]
    public async Task EvaluateAfterHumanApproval_SetsResumeContext()
    {
        var clock = new SystemClock();
        var run = AgentRun.Start(Guid.NewGuid(), "A", "O", "t", clock.UtcNow);
        var capture = new CapturingPolicyEvaluator();
        var svc = new ReviewPolicyReevaluationService(capture);
        var stepId = Guid.NewGuid();

        await svc.EvaluateAfterHumanApprovalAsync(
            run,
            stepId,
            WellKnownToolKeys.Pr1FakeTool,
            new Dictionary<string, string> { ["k"] = "v" },
            CancellationToken.None);

        Assert.NotNull(capture.LastRequest);
        Assert.True(capture.LastRequest!.Context?.ResumeAfterApprovedHumanReview);
    }

    [Fact]
    public async Task EvaluateResumedPlanStep_DoesNotSetResumeContext()
    {
        var clock = new SystemClock();
        var run = AgentRun.Start(Guid.NewGuid(), "A", "O", "t", clock.UtcNow);
        var capture = new CapturingPolicyEvaluator();
        var svc = new ReviewPolicyReevaluationService(capture);
        var stepId = Guid.NewGuid();

        await svc.EvaluateResumedPlanStepAsync(
            run,
            stepId,
            WellKnownToolKeys.Pr1FakeTool,
            new Dictionary<string, string>(),
            CancellationToken.None);

        Assert.NotNull(capture.LastRequest);
        Assert.Null(capture.LastRequest!.Context);
    }
}
