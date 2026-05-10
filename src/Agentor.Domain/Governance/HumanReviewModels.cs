namespace Agentor.Domain.Governance;

public enum ReviewDecisionKind
{
    Approve,
    Reject,
    RequestChanges,
    Escalate
}

public enum ReviewResolutionStatus
{
    Pending,
    ResolvedApproved,
    ResolvedRejected,
    ChangesRequested,
    Escalated
}

public sealed record HumanReviewDecision(
    Guid Id,
    ReviewDecisionKind Kind,
    Guid ActorId,
    DateTimeOffset DecidedAt,
    string? Note,
    ReviewResolutionStatus Resolution);