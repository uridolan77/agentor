using Agentor.Domain.Enums;

namespace Agentor.Contracts;

public sealed record AgentStepDto(
    Guid Id,
    int Index,
    string Name,
    AgentStepStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<PolicyDecisionDto> PolicyDecisions,
    IReadOnlyList<ToolCallDto> ToolCalls);
