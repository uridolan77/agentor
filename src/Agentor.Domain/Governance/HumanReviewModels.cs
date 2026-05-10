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

/// <summary>
/// Operator workflow state for human governance decisions (distinct from execution <see cref="Agentor.Domain.Enums.AgentRunStatus"/>).
/// </summary>
public enum HumanReviewWorkflowStatus
{
    None,
    Pending,
    ChangesRequested,
    Escalated,
    Approved,
    Rejected,
    Superseded
}

public sealed record HumanReviewDecision(
    Guid Id,
    ReviewDecisionKind Kind,
    Guid ActorId,
    DateTimeOffset DecidedAt,
    string? Note,
    ReviewResolutionStatus Resolution,
    Guid? RelatedPriorActorId = null);