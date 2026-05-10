using Agentor.Domain;

namespace Agentor.Application.Abstractions;

public interface IPolicyEvaluator
{
    Task<PolicyDecision> EvaluateToolCallAsync(PolicyEvaluationRequest request, CancellationToken cancellationToken);
}

public sealed record PolicyEvaluationRequest(
    Guid RunId,
    Guid StepId,
    string ToolKey,
    IReadOnlyDictionary<string, string> Input);
