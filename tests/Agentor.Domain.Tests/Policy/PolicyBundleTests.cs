using Agentor.Domain.Policy;
using Xunit;

namespace Agentor.Domain.Tests.Policy;

public sealed class PolicyBundleTests
{
    // ── PolicyBundleVersion ──────────────────────────────────────────────────

    [Fact]
    public void PolicyBundleVersion_Parse_ValidString_ReturnsCorrectVersion()
    {
        var v = PolicyBundleVersion.Parse("2.3");
        Assert.Equal(2, v.Major);
        Assert.Equal(3, v.Minor);
    }

    [Theory]
    [InlineData("0.0")]
    [InlineData("not-a-version")]
    [InlineData("1")]
    [InlineData("1.2.3")]
    [InlineData("")]
    public void PolicyBundleVersion_Parse_InvalidString_Throws(string input)
    {
        Assert.Throws<FormatException>(() => PolicyBundleVersion.Parse(input));
    }

    [Fact]
    public void PolicyBundleVersion_Equality_SameValues_Equal()
    {
        var a = new PolicyBundleVersion(1, 0);
        var b = new PolicyBundleVersion(1, 0);
        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void PolicyBundleVersion_Ordering_LowerMinorIsLess()
    {
        var lower = new PolicyBundleVersion(1, 0);
        var higher = new PolicyBundleVersion(1, 1);
        Assert.True(lower < higher);
        Assert.True(higher > lower);
    }

    [Fact]
    public void PolicyBundleVersion_ToString_ReturnsExpectedFormat()
    {
        Assert.Equal("1.0", new PolicyBundleVersion(1, 0).ToString());
        Assert.Equal("3.12", new PolicyBundleVersion(3, 12).ToString());
    }

    // ── PolicyRule ───────────────────────────────────────────────────────────

    [Fact]
    public void PolicyRule_EmptyId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            PolicyRule.ToolAllow(Guid.Empty, "my.tool"));
    }

    [Fact]
    public void PolicyRule_FactoryMethods_SetFieldsCorrectly()
    {
        var id = Guid.NewGuid();

        var allow = PolicyRule.ToolAllow(id, "tool.a");
        Assert.Equal(PolicyRuleKind.ToolAccess, allow.Kind);
        Assert.Equal(PolicyRuleEffect.Allow, allow.Effect);
        Assert.Equal("tool.a", allow.TargetKey);

        var deny = PolicyRule.ToolDeny(id, "tool.b");
        Assert.Equal(PolicyRuleEffect.Deny, deny.Effect);

        var review = PolicyRule.ToolRequiresReview(id, "tool.c");
        Assert.Equal(PolicyRuleEffect.RequiresReview, review.Effect);

        var mcpDeny = PolicyRule.McpToolDeny(id, "mcp.server.echo");
        Assert.Equal(PolicyRuleKind.McpToolAccess, mcpDeny.Kind);
        Assert.Equal(PolicyRuleEffect.Deny, mcpDeny.Effect);

        var extDeny = PolicyRule.ExternalAgentDeny(id, "external-agent.invoke");
        Assert.Equal(PolicyRuleKind.ExternalAgentAccess, extDeny.Kind);

        var costBudget = PolicyRule.ModelBudgetMaxCost(id, 5m);
        Assert.Equal(PolicyRuleKind.ModelBudget, costBudget.Kind);
        Assert.Equal("declaredCostUnits", costBudget.TargetKey);
        Assert.Equal("5", costBudget.ThresholdValue);

        var latencyBudget = PolicyRule.ModelBudgetMaxLatency(id, 200);
        Assert.Equal("declaredLatencyMs", latencyBudget.TargetKey);
        Assert.Equal("200", latencyBudget.ThresholdValue);
    }

    [Fact]
    public void PolicyRule_TenantScope_WithoutTenantId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new PolicyRule(
                Guid.NewGuid(),
                PolicyRuleKind.ToolAccess,
                PolicyRuleScope.Tenant,
                PolicyRuleEffect.Deny,
                "tool",
                null,
                "bad tenant scope"));
    }

    [Fact]
    public void PolicyRule_KnowledgeScope_WithExtraTenantId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new PolicyRule(
                Guid.NewGuid(),
                PolicyRuleKind.ToolAccess,
                PolicyRuleScope.KnowledgeScope,
                PolicyRuleEffect.Deny,
                "tool",
                null,
                "bad ks scope",
                scopeTenantId: Guid.NewGuid(),
                scopeKnowledgeScopeId: Guid.NewGuid()));
    }

    // ── PolicyBundle ─────────────────────────────────────────────────────────

    [Fact]
    public void PolicyBundle_Create_ValidInput_CreatesUnpublishedBundle()
    {
        var bundle = PolicyBundle.Create(
            Guid.NewGuid(),
            "My Bundle",
            PolicyBundleVersion.Initial,
            [PolicyRule.ToolAllow(Guid.NewGuid(), "my.tool")],
            DateTimeOffset.UtcNow);

        Assert.False(bundle.IsPublished);
        Assert.Null(bundle.PublishedAt);
        Assert.Single(bundle.Rules);
    }

    [Fact]
    public void PolicyBundle_Publish_SetsPublishedAtAndIsPublished()
    {
        var bundle = PolicyBundle.Create(
            Guid.NewGuid(), "B", PolicyBundleVersion.Initial, [], DateTimeOffset.UtcNow);
        var now = DateTimeOffset.UtcNow;
        bundle.Publish(now);

        Assert.True(bundle.IsPublished);
        Assert.Equal(now, bundle.PublishedAt);
    }

    [Fact]
    public void PolicyBundle_PublishTwice_Throws()
    {
        var bundle = PolicyBundle.Create(
            Guid.NewGuid(), "B", PolicyBundleVersion.Initial, [], DateTimeOffset.UtcNow);
        bundle.Publish(DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => bundle.Publish(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void PolicyBundle_DuplicateRuleIds_Throws()
    {
        var id = Guid.NewGuid();
        var rules = new[]
        {
            PolicyRule.ToolAllow(id, "tool.a"),
            PolicyRule.ToolDeny(id, "tool.b")
        };

        Assert.Throws<ArgumentException>(() =>
            PolicyBundle.Create(Guid.NewGuid(), "B", PolicyBundleVersion.Initial, rules, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void PolicyBundle_EmptyId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            PolicyBundle.Create(Guid.Empty, "B", PolicyBundleVersion.Initial, [], DateTimeOffset.UtcNow));
    }

    [Fact]
    public void PolicyBundle_EmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            PolicyBundle.Create(Guid.NewGuid(), "   ", PolicyBundleVersion.Initial, [], DateTimeOffset.UtcNow));
    }

    // ── PolicyProfile ────────────────────────────────────────────────────────

    [Fact]
    public void PolicyProfile_Create_ValidInput_HasNoBindings()
    {
        var profile = PolicyProfile.Create(Guid.NewGuid(), "Test Profile", DateTimeOffset.UtcNow);
        Assert.Empty(profile.Bindings);
        Assert.Null(profile.LatestBinding);
    }

    [Fact]
    public void PolicyProfile_BindToBundle_AddsBinding()
    {
        var profile = PolicyProfile.Create(Guid.NewGuid(), "P", DateTimeOffset.UtcNow);
        var bundleId = Guid.NewGuid();
        var version = new PolicyBundleVersion(1, 0);
        var now = DateTimeOffset.UtcNow;

        profile.BindToBundle(bundleId, version, now);

        Assert.Single(profile.Bindings);
        Assert.Equal(bundleId, profile.LatestBinding!.BundleId);
        Assert.Equal(version, profile.LatestBinding.BundleVersion);
    }

    [Fact]
    public void PolicyProfile_MultipleBindings_LatestBindingIsLast()
    {
        var profile = PolicyProfile.Create(Guid.NewGuid(), "P", DateTimeOffset.UtcNow);
        var v1 = new PolicyBundleVersion(1, 0);
        var v2 = new PolicyBundleVersion(1, 1);
        var bundleId1 = Guid.NewGuid();
        var bundleId2 = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        profile.BindToBundle(bundleId1, v1, now);
        profile.BindToBundle(bundleId2, v2, now.AddSeconds(10));

        Assert.Equal(2, profile.Bindings.Count);
        Assert.Equal(bundleId2, profile.LatestBinding!.BundleId);
        Assert.Equal(v2, profile.LatestBinding.BundleVersion);
    }
}
