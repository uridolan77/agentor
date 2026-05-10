using System.Globalization;
using Agentor.Infrastructure;
using Agentor.Domain;
using Agentor.Domain.Policy;

namespace Agentor.Infrastructure.Policy;

/// <summary>
/// Converts a <see cref="PolicyBundle"/>'s rules into <see cref="RuntimePolicyOptions.PolicyProfileRules"/> for
/// <see cref="RuntimePolicyEvaluator"/>, filtering rules by <see cref="AgentRunScope"/> and merging conflicts using
/// specificity precedence (KnowledgeScope &gt; Project &gt; Workspace &gt; Tenant &gt; Global) and,
/// at equal specificity, Deny &gt; RequiresReview &gt; Allow for tool access rules.
/// </summary>
public static class PolicyBundleRulesAdapter
{
    /// <summary>Adapts all rules as if the run had no tenant/workspace/project/knowledge scope (only Global rules match).</summary>
    public static PolicyProfileRules ToProfileRules(PolicyBundle bundle) =>
        ToProfileRules(bundle, new AgentRunScope(null, null, null, null));

    public static PolicyProfileRules ToProfileRules(PolicyBundle bundle, AgentRunScope runScope)
    {
        var matching = bundle.Rules.Where(r => RuleMatchesRun(r, runScope)).ToList();
        var profile = new PolicyProfileRules();

        MergeToolAccessRules(matching, profile);
        MergeMcpDenies(matching, profile);
        MergeExternalDenies(matching, profile);
        MergeModelBudgets(matching, profile);

        return profile;
    }

    internal static bool RuleMatchesRun(PolicyRule rule, AgentRunScope run)
    {
        switch (rule.Scope)
        {
            case PolicyRuleScope.Global:
                return true;

            case PolicyRuleScope.Tenant:
                return rule.ScopeTenantId.HasValue
                    && run.TenantId.HasValue
                    && run.TenantId.Value == rule.ScopeTenantId.Value;

            case PolicyRuleScope.Workspace:
                return rule.ScopeTenantId.HasValue
                    && rule.ScopeWorkspaceId.HasValue
                    && run.TenantId.HasValue
                    && run.WorkspaceId.HasValue
                    && run.TenantId.Value == rule.ScopeTenantId.Value
                    && run.WorkspaceId.Value == rule.ScopeWorkspaceId.Value;

            case PolicyRuleScope.Project:
                return rule.ScopeTenantId.HasValue
                    && rule.ScopeWorkspaceId.HasValue
                    && rule.ScopeProjectId.HasValue
                    && run.TenantId.HasValue
                    && run.WorkspaceId.HasValue
                    && run.ProjectId.HasValue
                    && run.TenantId.Value == rule.ScopeTenantId.Value
                    && run.WorkspaceId.Value == rule.ScopeWorkspaceId.Value
                    && run.ProjectId.Value == rule.ScopeProjectId.Value;

            case PolicyRuleScope.KnowledgeScope:
                return rule.ScopeKnowledgeScopeId.HasValue
                    && run.KnowledgeScopeId.HasValue
                    && run.KnowledgeScopeId.Value == rule.ScopeKnowledgeScopeId.Value;

            default:
                return false;
        }
    }

    private static int SpecificityRank(PolicyRuleScope scope) => scope switch
    {
        PolicyRuleScope.Global => 0,
        PolicyRuleScope.Tenant => 1,
        PolicyRuleScope.Workspace => 2,
        PolicyRuleScope.Project => 3,
        PolicyRuleScope.KnowledgeScope => 4,
        _ => 0
    };

    private static int EffectPriority(PolicyRuleEffect effect) => effect switch
    {
        PolicyRuleEffect.Deny => 2,
        PolicyRuleEffect.RequiresReview => 1,
        PolicyRuleEffect.Allow => 0,
        _ => 0
    };

    private sealed record EffectWinner(int Specificity, int EffectPriority, PolicyRuleEffect Effect);

    private static void MergeToolAccessRules(IReadOnlyList<PolicyRule> matching, PolicyProfileRules profile)
    {
        var winners = new Dictionary<string, EffectWinner>(StringComparer.OrdinalIgnoreCase);

        foreach (var rule in matching)
        {
            if (rule.Kind != PolicyRuleKind.ToolAccess || string.IsNullOrWhiteSpace(rule.TargetKey))
            {
                continue;
            }

            var key = rule.TargetKey.Trim();
            var sp = SpecificityRank(rule.Scope);
            var ep = EffectPriority(rule.Effect);

            if (!winners.TryGetValue(key, out var w))
            {
                winners[key] = new EffectWinner(sp, ep, rule.Effect);
                continue;
            }

            if (sp > w.Specificity)
            {
                winners[key] = new EffectWinner(sp, ep, rule.Effect);
            }
            else if (sp == w.Specificity && ep > w.EffectPriority)
            {
                winners[key] = new EffectWinner(sp, ep, rule.Effect);
            }
        }

        foreach (var (key, w) in winners)
        {
            switch (w.Effect)
            {
                case PolicyRuleEffect.Allow:
                    profile.AllowedToolKeys.Add(key);
                    break;
                case PolicyRuleEffect.Deny:
                    profile.DeniedToolKeys.Add(key);
                    break;
                case PolicyRuleEffect.RequiresReview:
                    profile.RequiresReviewToolKeys.Add(key);
                    break;
            }
        }
    }

    private static void MergeMcpDenies(IReadOnlyList<PolicyRule> matching, PolicyProfileRules profile)
    {
        var winners = new Dictionary<string, EffectWinner>(StringComparer.OrdinalIgnoreCase);

        foreach (var rule in matching)
        {
            if (rule.Kind != PolicyRuleKind.McpToolAccess
                || rule.Effect != PolicyRuleEffect.Deny
                || string.IsNullOrWhiteSpace(rule.TargetKey))
            {
                continue;
            }

            var key = rule.TargetKey.Trim();
            var sp = SpecificityRank(rule.Scope);
            const int ep = 2; // deny-only kind here

            if (!winners.TryGetValue(key, out var w))
            {
                winners[key] = new EffectWinner(sp, ep, PolicyRuleEffect.Deny);
                continue;
            }

            if (sp > w.Specificity)
            {
                winners[key] = new EffectWinner(sp, ep, PolicyRuleEffect.Deny);
            }
        }

        foreach (var key in winners.Keys)
        {
            profile.McpDeniedToolKeys.Add(key);
        }
    }

    private static void MergeExternalDenies(IReadOnlyList<PolicyRule> matching, PolicyProfileRules profile)
    {
        var winners = new Dictionary<string, EffectWinner>(StringComparer.OrdinalIgnoreCase);

        foreach (var rule in matching)
        {
            if (rule.Kind != PolicyRuleKind.ExternalAgentAccess
                || rule.Effect != PolicyRuleEffect.Deny
                || string.IsNullOrWhiteSpace(rule.TargetKey))
            {
                continue;
            }

            var key = rule.TargetKey.Trim();
            var sp = SpecificityRank(rule.Scope);
            const int ep = 2;

            if (!winners.TryGetValue(key, out var w))
            {
                winners[key] = new EffectWinner(sp, ep, PolicyRuleEffect.Deny);
                continue;
            }

            if (sp > w.Specificity)
            {
                winners[key] = new EffectWinner(sp, ep, PolicyRuleEffect.Deny);
            }
        }

        foreach (var key in winners.Keys)
        {
            profile.ExternalAgentDeniedToolKeys.Add(key);
        }
    }

    private static void MergeModelBudgets(IReadOnlyList<PolicyRule> matching, PolicyProfileRules profile)
    {
        PolicyRule? costWinner = null;
        PolicyRule? latencyWinner = null;

        foreach (var rule in matching.Where(r => r.Kind == PolicyRuleKind.ModelBudget))
        {
            if (rule.TargetKey == "declaredCostUnits")
            {
                PickStricterCost(ref costWinner, rule);
            }
            else if (rule.TargetKey == "declaredLatencyMs")
            {
                PickStricterLatency(ref latencyWinner, rule);
            }
        }

        if (costWinner?.ThresholdValue is not null
            && decimal.TryParse(costWinner.ThresholdValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var cost))
        {
            profile.MaxDeclaredModelCallCostUnits = cost;
        }

        if (latencyWinner?.ThresholdValue is not null
            && int.TryParse(latencyWinner.ThresholdValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lat))
        {
            profile.MaxDeclaredModelCallLatencyMs = lat;
        }
    }

    private static void PickStricterCost(ref PolicyRule? winner, PolicyRule candidate)
    {
        if (winner is null)
        {
            winner = candidate;
            return;
        }

        var wr = SpecificityRank(winner.Scope);
        var cr = SpecificityRank(candidate.Scope);
        if (cr > wr)
        {
            winner = candidate;
            return;
        }

        if (cr < wr)
        {
            return;
        }

        static decimal Parse(PolicyRule r) =>
            decimal.TryParse(r.ThresholdValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var d)
                ? d
                : decimal.MaxValue;

        if (Parse(candidate) < Parse(winner))
        {
            winner = candidate;
        }
    }

    private static void PickStricterLatency(ref PolicyRule? winner, PolicyRule candidate)
    {
        if (winner is null)
        {
            winner = candidate;
            return;
        }

        var wr = SpecificityRank(winner.Scope);
        var cr = SpecificityRank(candidate.Scope);
        if (cr > wr)
        {
            winner = candidate;
            return;
        }

        if (cr < wr)
        {
            return;
        }

        static int Parse(PolicyRule r) =>
            int.TryParse(r.ThresholdValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)
                ? i
                : int.MaxValue;

        if (Parse(candidate) < Parse(winner))
        {
            winner = candidate;
        }
    }
}
