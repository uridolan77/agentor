using Agentor.Domain;

namespace Agentor.Application.Abstractions;

public interface IPolicyEvaluator
{
    Task<PolicyDecision> EvaluateToolCallAsync(PolicyEvaluationRequest request, CancellationToken cancellationToken);
}

public sealed record PolicyEvaluationContext(bool ResumeAfterApprovedHumanReview = false);

public sealed record PolicyEvaluationRequest(
    Guid RunId,
    Guid StepId,
    string ToolKey,
    IReadOnlyDictionary<string, string> Input,
    PolicyEvaluationContext? Context = null);
