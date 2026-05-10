using Agentor.Contracts.KnowledgeState;

namespace Agentor.Application.Abstractions;

/// <summary>
/// Port to Athanor (canonical knowledge-state service). Implementations must not canonize from Agentor.
/// </summary>
public interface IKnowledgeStateClient
{
    Task<CanonicalSnapshotDto?> GetLatestSnapshotAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>Read-only canonical entry lookup for a stable key within the project scope.</summary>
    Task<CanonicalStateEntryDto?> LookupCanonicalEntryAsync(
        Guid projectId,
        string canonicalKey,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<EvidenceSearchResultDto>> SearchEvidenceAsync(
        Guid projectId,
        string query,
        CancellationToken cancellationToken);

    Task<CandidateSubmissionResultDto> SubmitCandidateAsync(
        Guid projectId,
        Guid agentRunId,
        CandidateKnowledgeSubmissionDto submission,
        CancellationToken cancellationToken);

    Task<ReviewQueueResultDto> QueueForReviewAsync(
        Guid projectId,
        Guid candidateId,
        Guid actorId,
        CancellationToken cancellationToken);
}