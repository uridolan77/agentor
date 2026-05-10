using Agentor.Application;
using Agentor.Application.Abstractions;
using Agentor.Domain;
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

/// <summary>Phase 26 — scoped policy bundles: tenant/workspace/project/knowledge filtering + precedence.</summary>
public sealed class PolicyScopeEvaluationTests
{
    private static PolicyBundle PublishedBundle(params PolicyRule[] rules)
    {
        var bundle = PolicyBundle.Create(
            Guid.NewGuid(), "Scoped Bundle", PolicyBundleVersion.Initial, rules, DateTimeOffset.UtcNow);
        bundle.Publish(DateTimeOffset.UtcNow);
        return bundle;
    }

    private static RuntimePolicyEvaluator BuildEvaluatorWithActiveBundle(PolicyBundle bundle, ActivePolicyProfile active)
    {
        var bundleRepo = new InMemoryPolicyBundleRepository();
        bundleRepo.SaveAsync(bundle, CancellationToken.None).GetAwaiter().GetResult();
        var profileRepo = new InMemoryPolicyProfileRepository();
        profileRepo.SetActiveAsync(active, CancellationToken.None).GetAwaiter().GetResult();

        var fake = new FakeToolExecutor();
        var registry = ToolRegistry.CreateDefault(
            fake,
            new FakeModelGatewayClient(),
            new FakeMcpRegistryClient(),
            new FakeA2AExternalAgentClient());
        var clock = new SystemClock();
        var opts = MSOptions.Create(new RuntimePolicyOptions());
        return new RuntimePolicyEvaluator(registry, clock, opts, bundleRepo, profileRepo);
    }

    private static ActivePolicyProfile ActiveFor(PolicyBundle bundle) =>
        new(Guid.NewGuid(), "test-profile", bundle.Id, bundle.Version, DateTimeOffset.UtcNow, Guid.NewGuid());

    private static PolicyEvaluationRequest Req(AgentRunScope scope, string toolKey = "") =>
        new(Guid.NewGuid(), Guid.NewGuid(), toolKey, new Dictionary<string, string>(), Scope: scope);

    [Fact]
    public async Task TenantDeny_DoesNotApplyToOtherTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var tool = WellKnownToolKeys.Pr1FakeTool;
        var bundle = PublishedBundle(
            new PolicyRule(
                Guid.NewGuid(),
                PolicyRuleKind.ToolAccess,
                PolicyRuleScope.Tenant,
                PolicyRuleEffect.Deny,
                tool,
                null,
                "deny A",
                scopeTenantId: tenantA));

        var eval = BuildEvaluatorWithActiveBundle(bundle, ActiveFor(bundle));

        var denyA = await eval.EvaluateToolCallAsync(Req(new AgentRunScope(tenantA, null, null, null), tool), CancellationToken.None);
        Assert.Equal(PolicyDecisionOutcome.Deny, denyA.Outcome);

        var allowB = await eval.EvaluateToolCallAsync(Req(new AgentRunScope(tenantB, null, null, null), tool), CancellationToken.None);
        Assert.Equal(PolicyDecisionOutcome.Allow, allowB.Outcome);
    }

    [Fact]
    public async Task WorkspaceRequiresReview_DoesNotApplyToSiblingWorkspace()
    {
        var tenant = Guid.NewGuid();
        var ws1 = Guid.NewGuid();
        var ws2 = Guid.NewGuid();
        var tool = WellKnownToolKeys.Pr1FakeTool;
        var bundle = PublishedBundle(
            new PolicyRule(
                Guid.NewGuid(),
                PolicyRuleKind.ToolAccess,
                PolicyRuleScope.Workspace,
                PolicyRuleEffect.RequiresReview,
                tool,
                null,
                "review ws1",
                scopeTenantId: tenant,
                scopeWorkspaceId: ws1));

        var eval = BuildEvaluatorWithActiveBundle(bundle, ActiveFor(bundle));

        var review = await eval.EvaluateToolCallAsync(
            Req(new AgentRunScope(tenant, ws1, null, null), tool),
            CancellationToken.None);
        Assert.Equal(PolicyDecisionOutcome.RequiresReview, review.Outcome);

        var allowSibling = await eval.EvaluateToolCallAsync(
            Req(new AgentRunScope(tenant, ws2, null, null), tool),
            CancellationToken.None);
        Assert.Equal(PolicyDecisionOutcome.Allow, allowSibling.Outcome);
    }

    [Fact]
    public async Task ProjectAllow_OverridesGlobalDeny_ForMatchingProject()
    {
        var tenant = Guid.NewGuid();
        var ws = Guid.NewGuid();
        var proj = Guid.NewGuid();
        var tool = WellKnownToolKeys.Pr1FakeTool;
        var bundle = PublishedBundle(
            new PolicyRule(Guid.NewGuid(), PolicyRuleKind.ToolAccess, PolicyRuleScope.Global, PolicyRuleEffect.Deny,
                tool, null, "global deny"),
            new PolicyRule(
                Guid.NewGuid(),
                PolicyRuleKind.ToolAccess,
                PolicyRuleScope.Project,
                PolicyRuleEffect.Allow,
                tool,
                null,
                "project allow",
                scopeTenantId: tenant,
                scopeWorkspaceId: ws,
                scopeProjectId: proj));

        var eval = BuildEvaluatorWithActiveBundle(bundle, ActiveFor(bundle));

        var scopedAllow = await eval.EvaluateToolCallAsync(
            Req(new AgentRunScope(tenant, ws, proj, null), tool),
            CancellationToken.None);
        Assert.Equal(PolicyDecisionOutcome.Allow, scopedAllow.Outcome);

        var globalDeny = await eval.EvaluateToolCallAsync(
            Req(new AgentRunScope(null, null, null, null), tool),
            CancellationToken.None);
        Assert.Equal(PolicyDecisionOutcome.Deny, globalDeny.Outcome);
    }

    [Fact]
    public async Task GlobalDeny_AppliesWhenNoMoreSpecificRuleMatchesTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var tool = WellKnownToolKeys.Pr1FakeTool;
        var bundle = PublishedBundle(
            new PolicyRule(Guid.NewGuid(), PolicyRuleKind.ToolAccess, PolicyRuleScope.Global, PolicyRuleEffect.Deny,
                tool, null, "global deny"),
            new PolicyRule(
                Guid.NewGuid(),
                PolicyRuleKind.ToolAccess,
                PolicyRuleScope.Tenant,
                PolicyRuleEffect.Allow,
                tool,
                null,
                "tenant B allow",
                scopeTenantId: tenantB));

        var eval = BuildEvaluatorWithActiveBundle(bundle, ActiveFor(bundle));

        var tenantADenied = await eval.EvaluateToolCallAsync(
            Req(new AgentRunScope(tenantA, null, null, null), tool),
            CancellationToken.None);
        Assert.Equal(PolicyDecisionOutcome.Deny, tenantADenied.Outcome);
    }

    [Fact]
    public void Adapter_TenantScopedRule_IgnoredWhenRunHasDifferentTenant()
    {
        var tenantA = Guid.NewGuid();
        var tool = WellKnownToolKeys.Pr1FakeTool;
        var bundle = PublishedBundle(
            new PolicyRule(
                Guid.NewGuid(),
                PolicyRuleKind.ToolAccess,
                PolicyRuleScope.Tenant,
                PolicyRuleEffect.Deny,
                tool,
                null,
                "A only",
                scopeTenantId: tenantA));

        var profileOther = PolicyBundleRulesAdapter.ToProfileRules(bundle, new AgentRunScope(Guid.NewGuid(), null, null, null));
        Assert.DoesNotContain(tool, profileOther.DeniedToolKeys);

        var profileMatch = PolicyBundleRulesAdapter.ToProfileRules(bundle, new AgentRunScope(tenantA, null, null, null));
        Assert.Contains(tool, profileMatch.DeniedToolKeys);
    }
}
