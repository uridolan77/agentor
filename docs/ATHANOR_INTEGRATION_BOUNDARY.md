# Athanor Integration Boundary

## Decision

Athanor remains a separate service. Agentor consumes Athanor through a client port (`IKnowledgeStateClient`). Phase 5 ships an in-process **fake** implementation; there is still **no** real Athanor HTTP client in this repository.

## Implemented port (Phase 5)

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

Implementations must **not** canonize knowledge from Agentor. Read, candidate submission, and review-queue operations are allowed; canonization remains Athanor’s authority after human or policy workflows there.

## Harness convention (temporary)

For Phase 5 alignment and tests, **`AgentRun.ProfileId` is passed as the Athanor `projectId`** argument on the port. This is a **harness convenience**, not a claim that Agentor’s agent profile identifier is always the same concept as an Athanor project or knowledge scope. Before wiring a real Athanor HTTP adapter, introduce an explicit **ProjectId**, **KnowledgeScopeId**, or similar on commands/DTOs as needed so production mapping is intentional rather than accidental.

## Rule

Athanor is not a library inside Agentor. It is an external authority.

## PR1

No Athanor runtime integration in PR1.
