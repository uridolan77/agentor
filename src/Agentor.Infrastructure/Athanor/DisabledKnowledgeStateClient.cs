using Agentor.Application.Abstractions;
using Agentor.Contracts.KnowledgeState;

namespace Agentor.Infrastructure.Athanor;

/// <summary>
/// Explicitly disabled Athanor adapter: every call fails with a stable operator-facing message.
/// </summary>
public sealed class DisabledKnowledgeStateClient : IKnowledgeStateClient
{
    private static readonly InvalidOperationException Error = new(
        "Athanor integration is disabled (configure Agentor:Integrations:Athanor:Mode).");

    public Task<CanonicalSnapshotDto?> GetLatestSnapshotAsync(Guid projectId, CancellationToken cancellationToken) =>
        Task.FromException<CanonicalSnapshotDto?>(Error);

    public Task<CanonicalStateEntryDto?> LookupCanonicalEntryAsync(
        Guid projectId,
        string canonicalKey,
        CancellationToken cancellationToken) =>
        Task.FromException<CanonicalStateEntryDto?>(Error);

    public Task<IReadOnlyList<EvidenceSearchResultDto>> SearchEvidenceAsync(
        Guid projectId,
        string query,
        CancellationToken cancellationToken) =>
        Task.FromException<IReadOnlyList<EvidenceSearchResultDto>>(Error);

    public Task<CandidateSubmissionResultDto> SubmitCandidateAsync(
        Guid projectId,
        Guid agentRunId,
        CandidateKnowledgeSubmissionDto submission,
        CancellationToken cancellationToken) =>
        Task.FromException<CandidateSubmissionResultDto>(Error);

    public Task<ReviewQueueResultDto> QueueForReviewAsync(
        Guid projectId,
        Guid candidateId,
        Guid actorId,
        CancellationToken cancellationToken) =>
        Task.FromException<ReviewQueueResultDto>(Error);
}
