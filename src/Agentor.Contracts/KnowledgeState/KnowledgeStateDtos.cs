namespace Agentor.Contracts.KnowledgeState;

public sealed record CanonicalStateEntryDto(string Key, string Value, double Confidence);

public sealed record CanonicalSnapshotDto(
    Guid SnapshotId,
    Guid ProjectId,
    DateTimeOffset CapturedAt,
    IReadOnlyList<CanonicalStateEntryDto> Entries);

public sealed record EvidenceSearchResultDto(Guid EvidenceId, string Title, string Snippet);

public sealed record CandidateKnowledgeSubmissionDto(string Summary, string PayloadJson);

public sealed record CandidateSubmissionResultDto(Guid CandidateId, string Status);

public sealed record ReviewQueueResultDto(Guid ReviewItemId, string Status);