using System.Collections.Concurrent;
using Agentor.Application.Abstractions;
using Agentor.Contracts.KnowledgeState;

namespace Agentor.Infrastructure.Athanor;

/// <summary>
/// Deterministic in-process stand-in for Athanor. No HTTP; no canonization.
/// </summary>
public sealed class FakeKnowledgeStateClient : IKnowledgeStateClient
{
    private readonly ConcurrentDictionary<Guid, CanonicalSnapshotDto> _latestSnapshots = new();
    private readonly ConcurrentDictionary<(Guid Project, string QueryNorm), List<EvidenceSearchResultDto>> _searchHits = new();
    private readonly ConcurrentDictionary<Guid, List<(Guid CandidateId, CandidateKnowledgeSubmissionDto Submission)>> _submissionsByRun = new();
    private readonly ConcurrentDictionary<(Guid Project, Guid CandidateId, Guid ActorId), ReviewQueueResultDto> _reviews = new();

    public void SeedLatestSnapshot(CanonicalSnapshotDto snapshot) => _latestSnapshots[snapshot.ProjectId] = snapshot;

    public void SeedSearchResults(Guid projectId, string query, IReadOnlyList<EvidenceSearchResultDto> hits) =>
        _searchHits[(projectId, Normalize(query))] = [..hits];

    public Task<CanonicalSnapshotDto?> GetLatestSnapshotAsync(Guid projectId, CancellationToken cancellationToken) =>
        Task.FromResult(_latestSnapshots.TryGetValue(projectId, out var s) ? s : null);

    public Task<CanonicalStateEntryDto?> LookupCanonicalEntryAsync(
        Guid projectId,
        string canonicalKey,
        CancellationToken cancellationToken)
    {
        if (!_latestSnapshots.TryGetValue(projectId, out var snap))
        {
            return Task.FromResult<CanonicalStateEntryDto?>(null);
        }

        var hit = snap.Entries.FirstOrDefault(e =>
            string.Equals(e.Key, canonicalKey, StringComparison.Ordinal));
        return Task.FromResult(hit);
    }

    public Task<IReadOnlyList<EvidenceSearchResultDto>> SearchEvidenceAsync(
        Guid projectId,
        string query,
        CancellationToken cancellationToken)
    {
        if (_searchHits.TryGetValue((projectId, Normalize(query)), out var list))
        {
            return Task.FromResult<IReadOnlyList<EvidenceSearchResultDto>>(list);
        }

        return Task.FromResult<IReadOnlyList<EvidenceSearchResultDto>>([]);
    }

    public Task<CandidateSubmissionResultDto> SubmitCandidateAsync(
        Guid projectId,
        Guid agentRunId,
        CandidateKnowledgeSubmissionDto submission,
        CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        var list = _submissionsByRun.GetOrAdd(agentRunId, _ => []);
        list.Add((id, submission));
        return Task.FromResult(new CandidateSubmissionResultDto(id, "recorded_non_canon"));
    }

    public Task<ReviewQueueResultDto> QueueForReviewAsync(
        Guid projectId,
        Guid candidateId,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        var key = (projectId, candidateId, actorId);
        if (_reviews.TryGetValue(key, out var existing))
        {
            return Task.FromResult(existing);
        }

        var dto = new ReviewQueueResultDto(Guid.NewGuid(), "queued");
        _reviews[key] = dto;
        return Task.FromResult(dto);
    }

    public IReadOnlyList<(Guid CandidateId, CandidateKnowledgeSubmissionDto Submission)> GetSubmissionsForRun(Guid agentRunId) =>
        _submissionsByRun.TryGetValue(agentRunId, out var list) ? list : [];

    private static string Normalize(string query) => query.Trim().ToLowerInvariant();
}