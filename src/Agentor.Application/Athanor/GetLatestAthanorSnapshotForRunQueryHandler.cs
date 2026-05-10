using Agentor.Application.Abstractions;
using Agentor.Contracts.KnowledgeState;
using Agentor.Domain;

namespace Agentor.Application.Athanor;

public sealed record AthanorSnapshotQueryResult(bool RunExists, CanonicalSnapshotDto? Snapshot);

public sealed class GetLatestAthanorSnapshotForRunQueryHandler(
    IAgentRunRepository repository,
    IKnowledgeStateClient knowledgeState)
{
    public async Task<AthanorSnapshotQueryResult> HandleAsync(Guid runId, CancellationToken cancellationToken)
    {
        var run = await repository.GetAsync(runId, cancellationToken);
        if (run is null)
        {
            return new AthanorSnapshotQueryResult(false, null);
        }

        var snapshot = await knowledgeState.GetLatestSnapshotAsync(run.ResolveAthanorProjectId(), cancellationToken);
        return new AthanorSnapshotQueryResult(true, snapshot);
    }
}