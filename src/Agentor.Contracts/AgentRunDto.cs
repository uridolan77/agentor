using Agentor.Domain.Enums;

namespace Agentor.Contracts;

public sealed record AgentRunDto(
    Guid Id,
    Guid ProfileId,
    string AgentName,
    string Objective,
    string TraceId,
    AgentRunStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string? ErrorMessage,
    IReadOnlyList<AgentStepDto> Steps,
    IReadOnlyList<TraceEventDto> Trace);
