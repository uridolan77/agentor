using Agentor.Application.Abstractions;
using Agentor.Domain;

namespace Agentor.Application.HumanReview;
/// <summary>
/// Wraps policy evaluation for tool calls that occur after human review approval or during plan resume.
/// </summary>
public sealed class ReviewPolicyReevaluationService(IPolicyEvaluator policyEvaluator)
{
    public Task<PolicyDecision> EvaluateAfterHumanApprovalAsync(
        AgentRun run,
        Guid stepId,
        string toolKey,
        IReadOnlyDictionary<string, string> input,
        CancellationToken cancellationToken) =>
        policyEvaluator.EvaluateToolCallAsync(
            new PolicyEvaluationRequest(
                run.Id,
                stepId,
                toolKey,
                input,
                new PolicyEvaluationContext(ResumeAfterApprovedHumanReview: true),
                run.ToPolicyScope()),
            cancellationToken);

    public Task<PolicyDecision> EvaluateResumedPlanStepAsync(
        AgentRun run,
        Guid stepId,
        string toolKey,
        IReadOnlyDictionary<string, string> input,
        CancellationToken cancellationToken) =>
        policyEvaluator.EvaluateToolCallAsync(
            new PolicyEvaluationRequest(run.Id, stepId, toolKey, input, Scope: run.ToPolicyScope()),
            cancellationToken);
}
