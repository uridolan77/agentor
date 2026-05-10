using Agentor.Application.Abstractions;
using Agentor.Domain;
using Agentor.Domain.Enums;

namespace Agentor.Application.Athanor;

public sealed class AttachAthanorEvidenceProvenanceHandler(
    IAgentRunRepository repository,
    IKnowledgeStateClient knowledgeState,
    IClock clock)
{
    public async Task<bool?> HandleAsync(Guid runId, string query, CancellationToken cancellationToken)
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

        var hits = await knowledgeState.SearchEvidenceAsync(run.ProfileId, query, cancellationToken);
        var ids = hits.Select(h => h.EvidenceId).ToList();
        run.AttachAthanorEvidenceSearchProvenance(query, ids, clock.UtcNow);
        await repository.SaveAsync(run, cancellationToken);
        return true;
    }
}