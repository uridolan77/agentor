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

    /// <summary>Tenant dimension when <see cref="Scope"/> is <see cref="PolicyRuleScope.Tenant"/> or narrower.</summary>
    public Guid? ScopeTenantId { get; }

    /// <summary>Workspace dimension when <see cref="Scope"/> is <see cref="PolicyRuleScope.Workspace"/> or narrower.</summary>
    public Guid? ScopeWorkspaceId { get; }

    /// <summary>Project dimension when <see cref="Scope"/> is <see cref="PolicyRuleScope.Project"/>.</summary>
    public Guid? ScopeProjectId { get; }

    /// <summary>Knowledge scope dimension when <see cref="Scope"/> is <see cref="PolicyRuleScope.KnowledgeScope"/>.</summary>
    public Guid? ScopeKnowledgeScopeId { get; }

    public PolicyRule(
        Guid id,
        PolicyRuleKind kind,
        PolicyRuleScope scope,
        PolicyRuleEffect effect,
        string? targetKey,
        string? thresholdValue,
        string description,
        Guid? scopeTenantId = null,
        Guid? scopeWorkspaceId = null,
        Guid? scopeProjectId = null,
        Guid? scopeKnowledgeScopeId = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Rule ID must not be empty.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(description, nameof(description));

        ValidateScopeIdentifiers(scope, scopeTenantId, scopeWorkspaceId, scopeProjectId, scopeKnowledgeScopeId);

        Id = id;
        Kind = kind;
        Scope = scope;
        Effect = effect;
        TargetKey = targetKey;
        ThresholdValue = thresholdValue;
        Description = description;
        ScopeTenantId = scopeTenantId;
        ScopeWorkspaceId = scopeWorkspaceId;
        ScopeProjectId = scopeProjectId;
        ScopeKnowledgeScopeId = scopeKnowledgeScopeId;
    }

    private static void ValidateScopeIdentifiers(
        PolicyRuleScope scope,
        Guid? tenantId,
        Guid? workspaceId,
        Guid? projectId,
        Guid? knowledgeScopeId)
    {
        switch (scope)
        {
            case PolicyRuleScope.Global:
                if (tenantId is not null || workspaceId is not null || projectId is not null || knowledgeScopeId is not null)
                {
                    throw new ArgumentException("Global scope rules must not specify scope identifiers.", nameof(scope));
                }

                break;

            case PolicyRuleScope.Tenant:
                if (!tenantId.HasValue || workspaceId is not null || projectId is not null || knowledgeScopeId is not null)
                {
                    throw new ArgumentException("Tenant scope requires ScopeTenantId only.", nameof(scope));
                }

                break;

            case PolicyRuleScope.Workspace:
                if (!tenantId.HasValue || !workspaceId.HasValue || projectId is not null || knowledgeScopeId is not null)
                {
                    throw new ArgumentException("Workspace scope requires ScopeTenantId and ScopeWorkspaceId.", nameof(scope));
                }

                break;

            case PolicyRuleScope.Project:
                if (!tenantId.HasValue || !workspaceId.HasValue || !projectId.HasValue || knowledgeScopeId is not null)
                {
                    throw new ArgumentException("Project scope requires ScopeTenantId, ScopeWorkspaceId, and ScopeProjectId.", nameof(scope));
                }

                break;

            case PolicyRuleScope.KnowledgeScope:
                if (!knowledgeScopeId.HasValue || tenantId is not null || workspaceId is not null || projectId is not null)
                {
                    throw new ArgumentException("KnowledgeScope scope requires ScopeKnowledgeScopeId only.", nameof(scope));
                }

                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown policy rule scope.");
        }
    }

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
