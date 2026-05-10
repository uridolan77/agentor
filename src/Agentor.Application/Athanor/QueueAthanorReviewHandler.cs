using Agentor.Application.Abstractions;
using Agentor.Domain;
using Agentor.Domain.Enums;

namespace Agentor.Application.Athanor;

public sealed class QueueAthanorReviewHandler(
    IAgentRunRepository repository,
    IKnowledgeStateClient knowledgeState,
    IClock clock)
{
    public async Task<bool?> HandleAsync(
        Guid runId,
        Guid candidateId,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        var run = await repository.GetAsync(runId, cancellationToken);
        if (run is null)
        {
            return null;
        }

        if (run.Status != AgentRunStatus.Running)
        {
            return false;
        }

        var result = await knowledgeState.QueueForReviewAsync(run.ResolveAthanorProjectId(), candidateId, actorId, cancellationToken);
        run.RecordAthanorReviewQueued(result.ReviewItemId, candidateId, actorId, clock.UtcNow);
        await repository.SaveAsync(run, cancellationToken);
        return true;
    }
}