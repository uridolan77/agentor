using Agentor.Application.Abstractions;
using Agentor.Contracts.KnowledgeState;
using Agentor.Domain;

namespace Agentor.Application.Athanor;

public sealed record AthanorCanonicalLookupResult(bool RunExists, CanonicalStateEntryDto? Entry);

public sealed class LookupAthanorCanonicalForRunQueryHandler(
    IAgentRunRepository repository,
    IKnowledgeStateClient knowledgeState)
{
    public async Task<AthanorCanonicalLookupResult> HandleAsync(
        Guid runId,
        string canonicalKey,
        CancellationToken cancellationToken)
    {
        var run = await repository.GetAsync(runId, cancellationToken);
        if (run is null)
        {
            return new AthanorCanonicalLookupResult(false, null);
        }

        if (string.IsNullOrWhiteSpace(canonicalKey))
        {
            throw new ArgumentException("Canonical key is required.", nameof(canonicalKey));
        }

        var entry = await knowledgeState.LookupCanonicalEntryAsync(run.ResolveAthanorProjectId(), canonicalKey.Trim(), cancellationToken);
        return new AthanorCanonicalLookupResult(true, entry);
    }
}