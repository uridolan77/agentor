using Agentor.Application.Abstractions;
using Agentor.Domain;
using Agentor.Domain.Enums;

namespace Agentor.Infrastructure;

public sealed class AllowAllPolicyEvaluator : IPolicyEvaluator
{
    private readonly IClock _clock;

    public AllowAllPolicyEvaluator(IClock clock)
    {
        _clock = clock;
    }

    public Task<PolicyDecision> EvaluateToolCallAsync(PolicyEvaluationRequest request, CancellationToken cancellationToken)
    {
        var decision = new PolicyDecision(
            Guid.NewGuid(),
            request.RunId,
            request.StepId,
            PolicyDecisionOutcome.Allow,
            "PR1_ALLOW_ALL",
            "PR1 uses an allow-all evaluator while preserving policy decision shape.",
            _clock.UtcNow);

        return Task.FromResult(decision);
    }
}
