using Agentor.Domain.Governance;

namespace Agentor.Contracts;

public sealed record ApplyHumanReviewRequestDto(
    ReviewDecisionKind Kind,
    string? Note = null,
    Guid? RelatedPriorActorId = null);

public sealed record HumanReviewDecisionDto(
    Guid Id,
    ReviewDecisionKind Kind,
    Guid ActorId,
    DateTimeOffset DecidedAt,
    string? Note,
    ReviewResolutionStatus Resolution,
    Guid? RelatedPriorActorId = null);