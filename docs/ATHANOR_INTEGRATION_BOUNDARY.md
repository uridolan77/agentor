# Athanor Integration Boundary

## Decision

Athanor remains a separate service. Agentor consumes Athanor through a client port.

## Future port

```csharp
public interface IKnowledgeStateClient
{
    Task<CanonicalSnapshotDto?> GetLatestSnapshotAsync(Guid projectId, CancellationToken ct);
    Task<CanonicalStateEntryDto?> LookupCanonicalEntryAsync(Guid projectId, string canonicalKey, CancellationToken ct);
    Task<IReadOnlyList<EvidenceSearchResultDto>> SearchEvidenceAsync(Guid projectId, string query, CancellationToken ct);
    Task<CandidateSubmissionResultDto> SubmitCandidateAsync(Guid projectId, Guid agentRunId, CandidateKnowledgeSubmissionDto submission, CancellationToken ct);
    Task<ReviewQueueResultDto> QueueForReviewAsync(Guid projectId, Guid candidateId, Guid actorId, CancellationToken ct);
}
```

## Rule

Athanor is not a library inside Agentor. It is an external authority.

## PR1

No Athanor runtime integration in PR1.
