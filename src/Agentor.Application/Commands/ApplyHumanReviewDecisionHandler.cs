using Agentor.Application.Abstractions;
using Agentor.Application.HumanReview;
using Agentor.Domain;
using Agentor.Domain.Enums;
using Agentor.Domain.Governance;

namespace Agentor.Application.Commands;

public sealed record ApplyHumanReviewDecisionCommand(
    Guid RunId,
    ReviewDecisionKind Kind,
    string? Note,
    Guid? RelatedPriorActorId = null);

public sealed class ApplyHumanReviewDecisionHandler(
    IAgentRunRepository repository,
    HumanReviewDecisionApplicator decisionApplicator,
    ReviewedToolContinuationService reviewedToolContinuation)
{
    public async Task<AgentRun?> HandleAsync(ApplyHumanReviewDecisionCommand command, CancellationToken cancellationToken)
    {
        var run = await repository.GetAsync(command.RunId, cancellationToken);
        if (run is null)
        {
            return null;
        }

        decisionApplicator.Apply(run, command);

        if (command.Kind == ReviewDecisionKind.Approve && run.Status == AgentRunStatus.Running)
        {
            await reviewedToolContinuation.ContinueApprovedToolExecutionAsync(run, cancellationToken);
        }

        await repository.SaveAsync(run, cancellationToken);
        return run;
    }
}
