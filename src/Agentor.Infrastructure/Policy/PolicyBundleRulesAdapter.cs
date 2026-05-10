using System.Globalization;
using Agentor.Domain.Policy;

namespace Agentor.Infrastructure.Policy;

/// <summary>
/// Converts a <see cref="PolicyBundle"/>'s rules into a <see cref="PolicyProfileRules"/> that
/// <see cref="RuntimePolicyEvaluator"/> can consume directly.
/// </summary>
/// <remarks>
/// <b>Phase 17 scope limitation:</b> All rules are mapped to the flat profile regardless of their
/// <see cref="PolicyRuleScope"/> value. Tenant / Workspace / Project scoping is modeled on
/// <see cref="PolicyRule"/> but not yet enforced here (deferred item SCOPE-001). Every rule in the
/// bundle currently takes effect as if it were <see cref="PolicyRuleScope.Global"/>.
/// </remarks>
public static class PolicyBundleRulesAdapter
{
    public static PolicyProfileRules ToProfileRules(PolicyBundle bundle)
    {
        var profile = new PolicyProfileRules();

        // SCOPE-001: rule.Scope is not evaluated; all rules are applied globally.
        foreach (var rule in bundle.Rules)
        {
            switch (rule.Kind)
            {
                case PolicyRuleKind.ToolAccess when rule.TargetKey is not null:
                    switch (rule.Effect)
                    {
                        case PolicyRuleEffect.Allow:
                            profile.AllowedToolKeys.Add(rule.TargetKey);
                            break;
                        case PolicyRuleEffect.Deny:
                            profile.DeniedToolKeys.Add(rule.TargetKey);
                            break;
                        case PolicyRuleEffect.RequiresReview:
                            profile.RequiresReviewToolKeys.Add(rule.TargetKey);
                            break;
                    }
                    break;

                case PolicyRuleKind.McpToolAccess
                    when rule.Effect == PolicyRuleEffect.Deny && rule.TargetKey is not null:
                    profile.McpDeniedToolKeys.Add(rule.TargetKey);
                    break;

                case PolicyRuleKind.ExternalAgentAccess
                    when rule.Effect == PolicyRuleEffect.Deny && rule.TargetKey is not null:
                    profile.ExternalAgentDeniedToolKeys.Add(rule.TargetKey);
                    break;

                case PolicyRuleKind.ModelBudget when rule.ThresholdValue is not null:
                    if (rule.TargetKey == "declaredCostUnits"
                        && decimal.TryParse(rule.ThresholdValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var cost))
                    {
                        profile.MaxDeclaredModelCallCostUnits = cost;
                    }
                    else if (rule.TargetKey == "declaredLatencyMs"
                        && int.TryParse(rule.ThresholdValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var latency))
                    {
                        profile.MaxDeclaredModelCallLatencyMs = latency;
                    }
                    break;
            }
        }

        return profile;
    }
}
