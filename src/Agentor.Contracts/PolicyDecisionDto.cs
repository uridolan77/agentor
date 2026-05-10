using Agentor.Domain.Enums;

namespace Agentor.Contracts;

public sealed record PolicyDecisionDto(
    Guid Id,
    PolicyDecisionOutcome Outcome,
    string ReasonCode,
    string Reason,
    DateTimeOffset DecidedAt);
