using Agentor.Application;
using Agentor.Application.Abstractions;
using Agentor.Domain.Enums;
using Agentor.Domain.Policy;
using Agentor.Infrastructure;
using Agentor.Infrastructure.Conexus;
using Agentor.Infrastructure.ExternalAgents;
using Agentor.Infrastructure.Mcp;
using Agentor.Infrastructure.Policy;
using Xunit;
using MSOptions = Microsoft.Extensions.Options.Options;

namespace Agentor.Application.Tests.Policy;

/// <summary>
/// Proves bundle-driven allow / deny / review via the PolicyBundleRulesAdapter → RuntimePolicyEvaluator path.
/// Each test constructs a fixture bundle, adapts it to a PolicyProfileRules, and asserts the evaluator outcome.
/// </summary>
public sealed class PolicyBundleEvaluationTests
{
    private static RuntimePolicyEvaluator BuildEvaluator(PolicyProfileRules? activeProfile = null)
    {
        var fake = new FakeToolExecutor();
        var registry = ToolRegistry.CreateDefault(
            fake,
            new FakeModelGatewayClient(),
            new FakeMcpRegistryClient(),
            new FakeA2AExternalAgentClient());
        var clock = new SystemClock();
        var opts = MSOptions.Create(new RuntimePolicyOptions { ActiveProfile = activeProfile });
        return new RuntimePolicyEvaluator(registry, clock, opts);
    }

    private static PolicyBundle PublishedBundle(params PolicyRule[] rules)
    {
        var bundle = PolicyBundle.Create(
            Guid.NewGuid(), "Test Bundle", PolicyBundleVersion.Initial, rules, DateTimeOffset.UtcNow);
        bundle.Publish(DateTimeOffset.UtcNow);
        return bundle;
    }

    // ── Adapter: ToolAccess Allow ────────────────────────────────────────────

    [Fact]
    public void Adapter_ToolAllow_ProducesAllowedToolKey()
    {
        var bundle = PublishedBundle(PolicyRule.ToolAllow(Guid.NewGuid(), WellKnownToolKeys.Pr1FakeTool));
        var profile = PolicyBundleRulesAdapter.ToProfileRules(bundle);

        Assert.Contains(WellKnownToolKeys.Pr1FakeTool, profile.AllowedToolKeys);
        Assert.Empty(profile.DeniedToolKeys);
    }

    [Fact]
    public void Adapter_ToolDeny_ProducesDeniedToolKey()
    {
        var bundle = PublishedBundle(PolicyRule.ToolDeny(Guid.NewGuid(), WellKnownToolKeys.Pr1FakeTool));
        var profile = PolicyBundleRulesAdapter.ToProfileRules(bundle);

        Assert.Contains(WellKnownToolKeys.Pr1FakeTool, profile.DeniedToolKeys);
    }

    [Fact]
    public void Adapter_ToolRequiresReview_ProducesRequiresReviewKey()
    {
        var bundle = PublishedBundle(PolicyRule.ToolRequiresReview(Guid.NewGuid(), WellKnownToolKeys.Pr1FakeTool));
        var profile = PolicyBundleRulesAdapter.ToProfileRules(bundle);

        Assert.Contains(WellKnownToolKeys.Pr1FakeTool, profile.RequiresReviewToolKeys);
    }

    [Fact]
    public void Adapter_McpToolDeny_ProducesMcpDeniedKey()
    {
        var mcpKey = McpToolKeys.Format("demo-server", "echo");
        var bundle = PublishedBundle(PolicyRule.McpToolDeny(Guid.NewGuid(), mcpKey));
        var profile = PolicyBundleRulesAdapter.ToProfileRules(bundle);

        Assert.Contains(mcpKey, profile.McpDeniedToolKeys);
    }

    [Fact]
    public void Adapter_ExternalAgentDeny_ProducesExternalAgentDeniedKey()
    {
        var bundle = PublishedBundle(PolicyRule.ExternalAgentDeny(Guid.NewGuid(), ExternalAgentToolKeys.Invoke));
        var profile = PolicyBundleRulesAdapter.ToProfileRules(bundle);

        Assert.Contains(ExternalAgentToolKeys.Invoke, profile.ExternalAgentDeniedToolKeys);
    }

    [Fact]
    public void Adapter_ModelBudgetCost_SetsMaxCostUnits()
    {
        var bundle = PublishedBundle(PolicyRule.ModelBudgetMaxCost(Guid.NewGuid(), 10m));
        var profile = PolicyBundleRulesAdapter.ToProfileRules(bundle);

        Assert.Equal(10m, profile.MaxDeclaredModelCallCostUnits);
    }

    [Fact]
    public void Adapter_ModelBudgetLatency_SetsMaxLatencyMs()
    {
        var bundle = PublishedBundle(PolicyRule.ModelBudgetMaxLatency(Guid.NewGuid(), 500));
        var profile = PolicyBundleRulesAdapter.ToProfileRules(bundle);

        Assert.Equal(500, profile.MaxDeclaredModelCallLatencyMs);
    }

    // ── Evaluator: bundle-driven deny ────────────────────────────────────────

    [Fact]
    public async Task Evaluator_BundleDrivenDeny_DeniesListedTool()
    {
        var bundle = PublishedBundle(PolicyRule.ToolDeny(Guid.NewGuid(), WellKnownToolKeys.Pr1FakeTool));
        var profile = PolicyBundleRulesAdapter.ToProfileRules(bundle);
        var evaluator = BuildEvaluator(activeProfile: profile);

        var decision = await evaluator.EvaluateToolCallAsync(
            new PolicyEvaluationRequest(Guid.NewGuid(), Guid.NewGuid(), WellKnownToolKeys.Pr1FakeTool, new Dictionary<string, string>()),
            CancellationToken.None);

        Assert.Equal(PolicyDecisionOutcome.Deny, decision.Outcome);
        Assert.Equal("TOOL_DENIED", decision.ReasonCode);
    }

    // ── Evaluator: bundle-driven tool-level RequiresReview ───────────────────

    [Fact]
    public async Task Evaluator_BundleDrivenRequiresReview_RequiresReviewForListedTool()
    {
        var bundle = PublishedBundle(PolicyRule.ToolRequiresReview(Guid.NewGuid(), WellKnownToolKeys.Pr1FakeTool));
        var profile = PolicyBundleRulesAdapter.ToProfileRules(bundle);
        var evaluator = BuildEvaluator(activeProfile: profile);

        var decision = await evaluator.EvaluateToolCallAsync(
            new PolicyEvaluationRequest(Guid.NewGuid(), Guid.NewGuid(), WellKnownToolKeys.Pr1FakeTool, new Dictionary<string, string>()),
            CancellationToken.None);

        Assert.Equal(PolicyDecisionOutcome.RequiresReview, decision.Outcome);
        Assert.Equal("TOOL_REVIEW_REQUIRED", decision.ReasonCode);
    }

    [Fact]
    public async Task Evaluator_BundleDrivenRequiresReview_AfterHumanApproval_Allows()
    {
        var bundle = PublishedBundle(PolicyRule.ToolRequiresReview(Guid.NewGuid(), WellKnownToolKeys.Pr1FakeTool));
        var profile = PolicyBundleRulesAdapter.ToProfileRules(bundle);
        var evaluator = BuildEvaluator(activeProfile: profile);

        var decision = await evaluator.EvaluateToolCallAsync(
            new PolicyEvaluationRequest(
                Guid.NewGuid(), Guid.NewGuid(), WellKnownToolKeys.Pr1FakeTool, new Dictionary<string, string>(),
                new PolicyEvaluationContext(ResumeAfterApprovedHumanReview: true)),
            CancellationToken.None);

        Assert.Equal(PolicyDecisionOutcome.Allow, decision.Outcome);
    }

    // ── Evaluator: RequiresReview distinct from Deny ─────────────────────────

    [Fact]
    public async Task Evaluator_RequiresReviewIsDistinctFromDeny()
    {
        // A bundle with BOTH a deny and a review rule on different tools.
        var bundle = PublishedBundle(
            PolicyRule.ToolDeny(Guid.NewGuid(), WellKnownToolKeys.Pr1FakeTool),
            PolicyRule.ToolRequiresReview(Guid.NewGuid(), WellKnownToolKeys.Pr1HighRiskFakeTool));
        var profile = PolicyBundleRulesAdapter.ToProfileRules(bundle);
        var evaluator = BuildEvaluator(activeProfile: profile);

        var deniedDecision = await evaluator.EvaluateToolCallAsync(
            new PolicyEvaluationRequest(Guid.NewGuid(), Guid.NewGuid(), WellKnownToolKeys.Pr1FakeTool, new Dictionary<string, string>()),
            CancellationToken.None);
        var reviewDecision = await evaluator.EvaluateToolCallAsync(
            new PolicyEvaluationRequest(Guid.NewGuid(), Guid.NewGuid(), WellKnownToolKeys.Pr1HighRiskFakeTool, new Dictionary<string, string>()),
            CancellationToken.None);

        Assert.Equal(PolicyDecisionOutcome.Deny, deniedDecision.Outcome);
        Assert.Equal(PolicyDecisionOutcome.RequiresReview, reviewDecision.Outcome);
        // Verify they are distinct values
        Assert.NotEqual(deniedDecision.Outcome, reviewDecision.Outcome);
    }

    // ── Evaluator: bundle-driven MCP deny ───────────────────────────────────

    [Fact]
    public async Task Evaluator_BundleDrivenMcpDeny_DeniesListedMcpTool()
    {
        var mcpKey = McpToolKeys.Format("demo-server", "echo");
        var bundle = PublishedBundle(PolicyRule.McpToolDeny(Guid.NewGuid(), mcpKey));
        var profile = PolicyBundleRulesAdapter.ToProfileRules(bundle);
        var evaluator = BuildEvaluator(activeProfile: profile);

        var decision = await evaluator.EvaluateToolCallAsync(
            new PolicyEvaluationRequest(Guid.NewGuid(), Guid.NewGuid(), mcpKey, new Dictionary<string, string>()),
            CancellationToken.None);

        Assert.Equal(PolicyDecisionOutcome.Deny, decision.Outcome);
        Assert.Equal("MCP_TOOL_DENIED", decision.ReasonCode);
    }

    // ── Evaluator: bundle-driven external-agent deny ─────────────────────────

    [Fact]
    public async Task Evaluator_BundleDrivenExternalAgentDeny_DeniesListedAgent()
    {
        var bundle = PublishedBundle(PolicyRule.ExternalAgentDeny(Guid.NewGuid(), ExternalAgentToolKeys.Invoke));
        var profile = PolicyBundleRulesAdapter.ToProfileRules(bundle);
        var evaluator = BuildEvaluator(activeProfile: profile);

        var decision = await evaluator.EvaluateToolCallAsync(
            new PolicyEvaluationRequest(Guid.NewGuid(), Guid.NewGuid(), ExternalAgentToolKeys.Invoke, new Dictionary<string, string>()),
            CancellationToken.None);

        Assert.Equal(PolicyDecisionOutcome.Deny, decision.Outcome);
        Assert.Equal("EXTERNAL_AGENT_TOOL_DENIED", decision.ReasonCode);
    }

    // ── Evaluator: bundle-driven model budget ────────────────────────────────

    [Fact]
    public async Task Evaluator_BundleDrivenModelBudget_DeniesExcessiveCost()
    {
        var bundle = PublishedBundle(PolicyRule.ModelBudgetMaxCost(Guid.NewGuid(), 1m));
        var profile = PolicyBundleRulesAdapter.ToProfileRules(bundle);
        var evaluator = BuildEvaluator(activeProfile: profile);

        var decision = await evaluator.EvaluateToolCallAsync(
            new PolicyEvaluationRequest(
                Guid.NewGuid(), Guid.NewGuid(), WellKnownToolKeys.ConexusModelComplete,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["declaredCostUnits"] = "99" }),
            CancellationToken.None);

        Assert.Equal(PolicyDecisionOutcome.Deny, decision.Outcome);
        Assert.Equal("BUDGET_DECLARED_COST", decision.ReasonCode);
    }

    // ── Evaluator: risk rules still work ────────────────────────────────────

    [Fact]
    public async Task Evaluator_RiskRulesStillWork_WhenBundleHasNoRiskOverride()
    {
        // Bundle with only a cost rule; high-risk tool should still get TOOL_RISK_REVIEW.
        var bundle = PublishedBundle(PolicyRule.ModelBudgetMaxCost(Guid.NewGuid(), 100m));
        var profile = PolicyBundleRulesAdapter.ToProfileRules(bundle);
        profile.MaxAutoApproveRisk = nameof(ToolRiskLevel.Low); // force low threshold
        var evaluator = BuildEvaluator(activeProfile: profile);

        var decision = await evaluator.EvaluateToolCallAsync(
            new PolicyEvaluationRequest(Guid.NewGuid(), Guid.NewGuid(), WellKnownToolKeys.Pr1HighRiskFakeTool, new Dictionary<string, string>()),
            CancellationToken.None);

        Assert.Equal(PolicyDecisionOutcome.RequiresReview, decision.Outcome);
        Assert.Equal("TOOL_RISK_REVIEW", decision.ReasonCode);
    }

    // ── Fallback path: no bundle active → RuntimePolicyOptions still works ───

    [Fact]
    public async Task Evaluator_NoActiveBundle_FallsBackToRuntimeOptions()
    {
        // No active profile → uses raw RuntimePolicyOptions which allow all by default.
        var evaluator = BuildEvaluator(activeProfile: null);

        var decision = await evaluator.EvaluateToolCallAsync(
            new PolicyEvaluationRequest(Guid.NewGuid(), Guid.NewGuid(), WellKnownToolKeys.Pr1FakeTool, new Dictionary<string, string>()),
            CancellationToken.None);

        Assert.Equal(PolicyDecisionOutcome.Allow, decision.Outcome);
    }
}
