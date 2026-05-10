using Agentor.Application;
using Agentor.Application.Abstractions;
using Agentor.Domain.Enums;
using Agentor.Infrastructure;
using Microsoft.Extensions.Options;
using Xunit;

namespace Agentor.Application.Tests;

public sealed class RuntimePolicyEvaluatorTests
{
    [Fact]
    public async Task Evaluate_AllowsDefaultLowRiskTool()
    {
        var fake = new FakeToolExecutor();
        var registry = ToolRegistry.CreateDefault(fake);
        var clock = new SystemClock();
        var policy = new RuntimePolicyEvaluator(registry, clock, Microsoft.Extensions.Options.Options.Create(new RuntimePolicyOptions()));

        var decision = await policy.EvaluateToolCallAsync(
            new PolicyEvaluationRequest(Guid.NewGuid(), Guid.NewGuid(), WellKnownToolKeys.Pr1FakeTool, new Dictionary<string, string>()),
            CancellationToken.None);

        Assert.Equal(PolicyDecisionOutcome.Allow, decision.Outcome);
    }

    [Fact]
    public async Task Evaluate_DeniesWhenToolOnDenyList()
    {
        var fake = new FakeToolExecutor();
        var registry = ToolRegistry.CreateDefault(fake);
        var clock = new SystemClock();
        var opts = Microsoft.Extensions.Options.Options.Create(new RuntimePolicyOptions
        {
            DeniedToolKeys = [WellKnownToolKeys.Pr1FakeTool]
        });
        var policy = new RuntimePolicyEvaluator(registry, clock, opts);

        var decision = await policy.EvaluateToolCallAsync(
            new PolicyEvaluationRequest(Guid.NewGuid(), Guid.NewGuid(), WellKnownToolKeys.Pr1FakeTool, new Dictionary<string, string>()),
            CancellationToken.None);

        Assert.Equal(PolicyDecisionOutcome.Deny, decision.Outcome);
    }

    [Fact]
    public async Task Evaluate_RequiresReviewWhenRiskExceedsMaxAutoApprove()
    {
        var fake = new FakeToolExecutor();
        var registry = ToolRegistry.CreateDefault(fake);
        var clock = new SystemClock();
        var opts = Microsoft.Extensions.Options.Options.Create(new RuntimePolicyOptions
        {
            MaxAutoApproveRisk = nameof(ToolRiskLevel.Low)
        });
        var policy = new RuntimePolicyEvaluator(registry, clock, opts);

        var decision = await policy.EvaluateToolCallAsync(
            new PolicyEvaluationRequest(Guid.NewGuid(), Guid.NewGuid(), WellKnownToolKeys.Pr1HighRiskFakeTool, new Dictionary<string, string>()),
            CancellationToken.None);

        Assert.Equal(PolicyDecisionOutcome.RequiresReview, decision.Outcome);
    }

    [Fact]
    public async Task Evaluate_DeniesUnknownToolKey()
    {
        var fake = new FakeToolExecutor();
        var registry = ToolRegistry.CreateDefault(fake);
        var clock = new SystemClock();
        var policy = new RuntimePolicyEvaluator(registry, clock, Microsoft.Extensions.Options.Options.Create(new RuntimePolicyOptions()));

        var decision = await policy.EvaluateToolCallAsync(
            new PolicyEvaluationRequest(Guid.NewGuid(), Guid.NewGuid(), "no.such.tool", new Dictionary<string, string>()),
            CancellationToken.None);

        Assert.Equal(PolicyDecisionOutcome.Deny, decision.Outcome);
    }
}