using Agentor.Application.Abstractions;
using Agentor.Domain;
using Agentor.Domain.Enums;
using Microsoft.Extensions.Options;

namespace Agentor.Infrastructure;

public sealed class RuntimePolicyEvaluator : IPolicyEvaluator
{
    private readonly IToolRegistry _toolRegistry;
    private readonly IClock _clock;
    private readonly RuntimePolicyOptions _options;

    public RuntimePolicyEvaluator(
        IToolRegistry toolRegistry,
        IClock clock,
        IOptions<RuntimePolicyOptions> options)
    {
        _toolRegistry = toolRegistry;
        _clock = clock;
        _options = options.Value;
    }

    public Task<PolicyDecision> EvaluateToolCallAsync(PolicyEvaluationRequest request, CancellationToken cancellationToken)
    {
        if (!_toolRegistry.TryGetRegistration(request.ToolKey, out var reg) || reg is null)
        {
            return Task.FromResult(new PolicyDecision(
                Guid.NewGuid(),
                request.RunId,
                request.StepId,
                PolicyDecisionOutcome.Deny,
                "UNKNOWN_TOOL",
                $"Tool '{request.ToolKey}' is not registered.",
                _clock.UtcNow));
        }

        var definition = reg.Definition;

        if (_options.DeniedToolKeys.Exists(k => string.Equals(k, request.ToolKey, StringComparison.OrdinalIgnoreCase)))
        {
            return Task.FromResult(new PolicyDecision(
                Guid.NewGuid(),
                request.RunId,
                request.StepId,
                PolicyDecisionOutcome.Deny,
                "TOOL_DENIED",
                $"Tool '{request.ToolKey}' is denied by runtime policy.",
                _clock.UtcNow));
        }

        if (_options.AllowedToolKeys.Count > 0
            && !_options.AllowedToolKeys.Exists(k => string.Equals(k, request.ToolKey, StringComparison.OrdinalIgnoreCase)))
        {
            return Task.FromResult(new PolicyDecision(
                Guid.NewGuid(),
                request.RunId,
                request.StepId,
                PolicyDecisionOutcome.Deny,
                "TOOL_NOT_ALLOWED",
                $"Tool '{request.ToolKey}' is not on the runtime allow list.",
                _clock.UtcNow));
        }

        var maxRisk = ParseRisk(_options.MaxAutoApproveRisk);
        if (CompareRisk(definition.RiskLevel, maxRisk) > 0)
        {
            return Task.FromResult(new PolicyDecision(
                Guid.NewGuid(),
                request.RunId,
                request.StepId,
                PolicyDecisionOutcome.RequiresReview,
                "TOOL_RISK_REVIEW",
                $"Tool '{request.ToolKey}' requires review before execution.",
                _clock.UtcNow));
        }

        return Task.FromResult(new PolicyDecision(
            Guid.NewGuid(),
            request.RunId,
            request.StepId,
            PolicyDecisionOutcome.Allow,
            "RUNTIME_ALLOW",
            "Runtime policy allows this tool call.",
            _clock.UtcNow));
    }

    private static ToolRiskLevel ParseRisk(string value)
    {
        return Enum.TryParse<ToolRiskLevel>(value, ignoreCase: true, out var r)
            ? r
            : ToolRiskLevel.High;
    }

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
}