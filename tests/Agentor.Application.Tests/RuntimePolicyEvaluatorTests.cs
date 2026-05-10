using Agentor.Application;
using Agentor.Application.Abstractions;
using Agentor.Domain.Enums;
using Agentor.Infrastructure;
using Agentor.Infrastructure.Conexus;
using Microsoft.Extensions.Options;
using Xunit;

namespace Agentor.Application.Tests;

public sealed class RuntimePolicyEvaluatorTests
{
    [Fact]
    public async Task Evaluate_AllowsDefaultLowRiskTool()
    {
        var fake = new FakeToolExecutor();
        var registry = ToolRegistry.CreateDefault(fake, new FakeModelGatewayClient());
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
        var registry = ToolRegistry.CreateDefault(fake, new FakeModelGatewayClient());
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
        var registry = ToolRegistry.CreateDefault(fake, new FakeModelGatewayClient());
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
        var registry = ToolRegistry.CreateDefault(fake, new FakeModelGatewayClient());
        var clock = new SystemClock();
        var policy = new RuntimePolicyEvaluator(registry, clock, Microsoft.Extensions.Options.Options.Create(new RuntimePolicyOptions()));

        var decision = await policy.EvaluateToolCallAsync(
            new PolicyEvaluationRequest(Guid.NewGuid(), Guid.NewGuid(), "no.such.tool", new Dictionary<string, string>()),
            CancellationToken.None);

        Assert.Equal(PolicyDecisionOutcome.Deny, decision.Outcome);
    }

    [Fact]
    public async Task Evaluate_DeniesModelCallWhenDeclaredCostExceedsCap()
    {
        var fake = new FakeToolExecutor();
        var registry = ToolRegistry.CreateDefault(fake, new FakeModelGatewayClient());
        var clock = new SystemClock();
        var opts = Microsoft.Extensions.Options.Options.Create(new RuntimePolicyOptions
        {
            MaxDeclaredModelCallCostUnits = 1m
        });
        var policy = new RuntimePolicyEvaluator(registry, clock, opts);

        var decision = await policy.EvaluateToolCallAsync(
            new PolicyEvaluationRequest(
                Guid.NewGuid(),
                Guid.NewGuid(),
                WellKnownToolKeys.ConexusModelComplete,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["declaredCostUnits"] = "99"
                }),
            CancellationToken.None);

        Assert.Equal(PolicyDecisionOutcome.Deny, decision.Outcome);
        Assert.Equal("BUDGET_DECLARED_COST", decision.ReasonCode);
    }

    [Fact]
    public async Task Evaluate_DeniesModelCallWhenDeclaredLatencyExceedsCap()
    {
        var fake = new FakeToolExecutor();
        var registry = ToolRegistry.CreateDefault(fake, new FakeModelGatewayClient());
        var clock = new SystemClock();
        var opts = Microsoft.Extensions.Options.Options.Create(new RuntimePolicyOptions
        {
            MaxDeclaredModelCallLatencyMs = 100
        });
        var policy = new RuntimePolicyEvaluator(registry, clock, opts);

        var decision = await policy.EvaluateToolCallAsync(
            new PolicyEvaluationRequest(
                Guid.NewGuid(),
                Guid.NewGuid(),
                WellKnownToolKeys.ConexusModelComplete,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["declaredLatencyMs"] = "500"
                }),
            CancellationToken.None);

        Assert.Equal(PolicyDecisionOutcome.Deny, decision.Outcome);
        Assert.Equal("BUDGET_DECLARED_LATENCY", decision.ReasonCode);
    }
}