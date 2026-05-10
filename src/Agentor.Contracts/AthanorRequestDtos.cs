namespace Agentor.Contracts;

public sealed record AttachEvidenceProvenanceRequestDto(string Query);

public sealed record SubmitAthanorCandidateRequestDto(string Summary, string PayloadJson);

public sealed record QueueAthanorReviewRequestDto(Guid CandidateId, Guid ActorId);