using System.Globalization;
using Agentor.Application;
using Agentor.Application.Abstractions;
using Agentor.Domain;
using Agentor.Domain.Enums;
using Agentor.Domain.Policy;
using Agentor.Infrastructure.Policy;
using Microsoft.Extensions.Options;

namespace Agentor.Infrastructure;

public sealed class RuntimePolicyEvaluator : IPolicyEvaluator
{
    private readonly IToolRegistry _toolRegistry;
    private readonly IClock _clock;
    private readonly RuntimePolicyOptions _options;
    private readonly IPolicyBundleRepository _bundleRepo;
    private readonly IPolicyProfileRepository _profileRepo;

    // Lightweight constructor for direct instantiation in unit tests.
    // Falls back to config-only evaluation (no bundle awareness).
    public RuntimePolicyEvaluator(
        IToolRegistry toolRegistry,
        IClock clock,
        IOptions<RuntimePolicyOptions> options)
        : this(toolRegistry, clock, options, NullBundleRepo.Instance, NullProfileRepo.Instance)
    {
    }

    // Full constructor used by DI when bundle/profile repos are registered.
    public RuntimePolicyEvaluator(
        IToolRegistry toolRegistry,
        IClock clock,
        IOptions<RuntimePolicyOptions> options,
        IPolicyBundleRepository bundleRepo,
        IPolicyProfileRepository profileRepo)
    {
        _toolRegistry = toolRegistry;
        _clock = clock;
        _options = options.Value;
        _bundleRepo = bundleRepo;
        _profileRepo = profileRepo;
    }

    public async Task<PolicyDecision> EvaluateToolCallAsync(
        PolicyEvaluationRequest request,
        CancellationToken cancellationToken)
    {
        if (!_toolRegistry.TryGetRegistration(request.ToolKey, out var reg) || reg is null)
        {
            return Decision(request, PolicyDecisionOutcome.Deny,
                "UNKNOWN_TOOL", $"Tool '{request.ToolKey}' is not registered.");
        }

        var profile = await ResolveEffectiveProfileAsync(cancellationToken);
        return EvaluateWithProfile(request, reg.Definition, profile);
    }

    // Prefer the active bundle-derived profile over the config-level ActiveProfile.
    private async Task<PolicyProfileRules?> ResolveEffectiveProfileAsync(CancellationToken cancellationToken)
    {
        var active = await _profileRepo.GetActiveAsync(cancellationToken);
        if (active is not null)
        {
            var bundle = await _bundleRepo.GetAsync(active.BundleId, cancellationToken);
            if (bundle is not null)
            {
                return PolicyBundleRulesAdapter.ToProfileRules(bundle);
            }
        }

        return _options.ActiveProfile;
    }

    private PolicyDecision EvaluateWithProfile(
        PolicyEvaluationRequest request,
        ToolDefinition definition,
        PolicyProfileRules? profile)
    {
        var denied = profile?.DeniedToolKeys ?? _options.DeniedToolKeys;
        var allowed = profile?.AllowedToolKeys ?? _options.AllowedToolKeys;
        var requiresReview = profile?.RequiresReviewToolKeys ?? [];
        var maxAutoApproveRisk = profile?.MaxAutoApproveRisk ?? _options.MaxAutoApproveRisk;
        var maxCost = profile?.MaxDeclaredModelCallCostUnits ?? _options.MaxDeclaredModelCallCostUnits;
        var maxLatency = profile?.MaxDeclaredModelCallLatencyMs ?? _options.MaxDeclaredModelCallLatencyMs;

        // 1. Explicit deny list
        if (denied.Exists(k => string.Equals(k, request.ToolKey, StringComparison.OrdinalIgnoreCase)))
        {
            return Decision(request, PolicyDecisionOutcome.Deny,
                "TOOL_DENIED", $"Tool '{request.ToolKey}' is denied by runtime policy.");
        }

        // 2. MCP-specific deny
        if (profile?.McpDeniedToolKeys is { Count: > 0 } mcpDenied
            && McpToolKeys.IsMcpToolKey(request.ToolKey)
            && mcpDenied.Exists(k => string.Equals(k, request.ToolKey, StringComparison.OrdinalIgnoreCase)))
        {
            return Decision(request, PolicyDecisionOutcome.Deny,
                "MCP_TOOL_DENIED", $"MCP tool '{request.ToolKey}' is denied by policy profile.");
        }

        // 3. External-agent-specific deny
        if (profile?.ExternalAgentDeniedToolKeys is { Count: > 0 } extDenied
            && ExternalAgentToolKeys.IsExternalAgentTool(request.ToolKey)
            && extDenied.Exists(k => string.Equals(k, request.ToolKey, StringComparison.OrdinalIgnoreCase)))
        {
            return Decision(request, PolicyDecisionOutcome.Deny,
                "EXTERNAL_AGENT_TOOL_DENIED", $"External-agent tool '{request.ToolKey}' is denied by policy profile.");
        }

        // 4. Allow-list enforcement (deny anything not listed when list is non-empty)
        if (allowed.Count > 0
            && !allowed.Exists(k => string.Equals(k, request.ToolKey, StringComparison.OrdinalIgnoreCase)))
        {
            return Decision(request, PolicyDecisionOutcome.Deny,
                "TOOL_NOT_ALLOWED", $"Tool '{request.ToolKey}' is not on the runtime allow list.");
        }

        // 5. Declared model-call budget gates (pre-execution intent checks only)
        if (string.Equals(request.ToolKey, WellKnownToolKeys.ConexusModelComplete, StringComparison.OrdinalIgnoreCase))
        {
            if (maxCost is { } maxCostVal
                && TryParseDecimal(request.Input, "declaredCostUnits", out var declaredCost)
                && declaredCost > maxCostVal)
            {
                return Decision(request, PolicyDecisionOutcome.Deny,
                    "BUDGET_DECLARED_COST",
                    $"Declared model-call cost {declaredCost} exceeds policy maximum {maxCostVal}.");
            }

            if (maxLatency is { } maxLatencyVal
                && TryParseInt(request.Input, "declaredLatencyMs", out var declaredLatency)
                && declaredLatency > maxLatencyVal)
            {
                return Decision(request, PolicyDecisionOutcome.Deny,
                    "BUDGET_DECLARED_LATENCY",
                    $"Declared model-call latency {declaredLatency} ms exceeds policy maximum {maxLatencyVal} ms.");
            }
        }

        // 6. Bundle-driven per-tool RequiresReview (distinct from Deny)
        if (requiresReview.Count > 0
            && requiresReview.Exists(k => string.Equals(k, request.ToolKey, StringComparison.OrdinalIgnoreCase))
            && request.Context?.ResumeAfterApprovedHumanReview != true)
        {
            return Decision(request, PolicyDecisionOutcome.RequiresReview,
                "TOOL_REVIEW_REQUIRED", $"Tool '{request.ToolKey}' requires review by bundle policy.");
        }

        // 7. Risk-level threshold
        var maxRisk = ParseRisk(maxAutoApproveRisk);
        if (CompareRisk(definition.RiskLevel, maxRisk) > 0
            && request.Context?.ResumeAfterApprovedHumanReview != true)
        {
            return Decision(request, PolicyDecisionOutcome.RequiresReview,
                "TOOL_RISK_REVIEW", $"Tool '{request.ToolKey}' requires review before execution.");
        }

        return Decision(request, PolicyDecisionOutcome.Allow,
            "RUNTIME_ALLOW", "Runtime policy allows this tool call.");
    }

    private PolicyDecision Decision(
        PolicyEvaluationRequest request,
        PolicyDecisionOutcome outcome,
        string reasonCode,
        string reason) =>
        new(Guid.NewGuid(), request.RunId, request.StepId, outcome, reasonCode, reason, _clock.UtcNow);

    private static ToolRiskLevel ParseRisk(string value) =>
        Enum.TryParse<ToolRiskLevel>(value, ignoreCase: true, out var r) ? r : ToolRiskLevel.High;

    private static int CompareRisk(ToolRiskLevel tool, ToolRiskLevel maxApproved)
    {
        static int Rank(ToolRiskLevel r) => r switch
        {
            ToolRiskLevel.Low => 0,
            ToolRiskLevel.Medium => 1,
            ToolRiskLevel.High => 2,
            _ => 0
        };
        return Rank(tool) - Rank(maxApproved);
    }

    private static bool TryParseDecimal(IReadOnlyDictionary<string, string> input, string key, out decimal value)
    {
        value = default;
        return input.TryGetValue(key, out var raw)
            && !string.IsNullOrWhiteSpace(raw)
            && decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseInt(IReadOnlyDictionary<string, string> input, string key, out int value)
    {
        value = default;
        return input.TryGetValue(key, out var raw)
            && !string.IsNullOrWhiteSpace(raw)
            && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    // No-op repos used by the 3-param constructor so existing tests need no changes.
    private sealed class NullBundleRepo : IPolicyBundleRepository
    {
        public static readonly NullBundleRepo Instance = new();

        public Task SaveAsync(PolicyBundle bundle, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<PolicyBundle?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<PolicyBundle?>(null);

        public Task<IReadOnlyList<PolicyBundle>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PolicyBundle>>([]);
    }

    private sealed class NullProfileRepo : IPolicyProfileRepository
    {
        public static readonly NullProfileRepo Instance = new();

        public Task<ActivePolicyProfile?> GetActiveAsync(CancellationToken cancellationToken) =>
            Task.FromResult<ActivePolicyProfile?>(null);

        public Task SetActiveAsync(ActivePolicyProfile active, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
