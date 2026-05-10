using System.Globalization;

namespace Agentor.Domain.Policy;

public sealed class PolicyRule
{
    public Guid Id { get; }
    public PolicyRuleKind Kind { get; }
    public PolicyRuleScope Scope { get; }
    public PolicyRuleEffect Effect { get; }

    /// <summary>Tool key, MCP key, or external-agent key. Null for aggregate-level budget rules.</summary>
    public string? TargetKey { get; }

    /// <summary>For ModelBudget rules: numeric threshold as invariant-culture string (cost units or latency ms).</summary>
    public string? ThresholdValue { get; }

    public string Description { get; }

    public PolicyRule(
        Guid id,
        PolicyRuleKind kind,
        PolicyRuleScope scope,
        PolicyRuleEffect effect,
        string? targetKey,
        string? thresholdValue,
        string description)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Rule ID must not be empty.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(description, nameof(description));

        Id = id;
        Kind = kind;
        Scope = scope;
        Effect = effect;
        TargetKey = targetKey;
        ThresholdValue = thresholdValue;
        Description = description;
    }

    // Factory helpers keep call sites readable.

    public static PolicyRule ToolAllow(Guid id, string toolKey, PolicyRuleScope scope = PolicyRuleScope.Global) =>
        new(id, PolicyRuleKind.ToolAccess, scope, PolicyRuleEffect.Allow, toolKey, null, $"Allow tool '{toolKey}'.");

    public static PolicyRule ToolDeny(Guid id, string toolKey, PolicyRuleScope scope = PolicyRuleScope.Global) =>
        new(id, PolicyRuleKind.ToolAccess, scope, PolicyRuleEffect.Deny, toolKey, null, $"Deny tool '{toolKey}'.");

    public static PolicyRule ToolRequiresReview(Guid id, string toolKey, PolicyRuleScope scope = PolicyRuleScope.Global) =>
        new(id, PolicyRuleKind.ToolAccess, scope, PolicyRuleEffect.RequiresReview, toolKey, null, $"Require review for tool '{toolKey}'.");

    public static PolicyRule McpToolDeny(Guid id, string mcpKey, PolicyRuleScope scope = PolicyRuleScope.Global) =>
        new(id, PolicyRuleKind.McpToolAccess, scope, PolicyRuleEffect.Deny, mcpKey, null, $"Deny MCP tool '{mcpKey}'.");

    public static PolicyRule ExternalAgentDeny(Guid id, string agentKey, PolicyRuleScope scope = PolicyRuleScope.Global) =>
        new(id, PolicyRuleKind.ExternalAgentAccess, scope, PolicyRuleEffect.Deny, agentKey, null, $"Deny external-agent '{agentKey}'.");

    public static PolicyRule ModelBudgetMaxCost(Guid id, decimal maxCostUnits, PolicyRuleScope scope = PolicyRuleScope.Global) =>
        new(id, PolicyRuleKind.ModelBudget, scope, PolicyRuleEffect.Deny,
            "declaredCostUnits",
            maxCostUnits.ToString(CultureInfo.InvariantCulture),
            $"Deny model calls with declared cost > {maxCostUnits}.");

    public static PolicyRule ModelBudgetMaxLatency(Guid id, int maxLatencyMs, PolicyRuleScope scope = PolicyRuleScope.Global) =>
        new(id, PolicyRuleKind.ModelBudget, scope, PolicyRuleEffect.Deny,
            "declaredLatencyMs",
            maxLatencyMs.ToString(CultureInfo.InvariantCulture),
            $"Deny model calls with declared latency > {maxLatencyMs} ms.");
}
