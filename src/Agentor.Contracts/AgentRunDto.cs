using Agentor.Domain.Enums;

namespace Agentor.Contracts;

public sealed record AgentRunDto(
    Guid Id,
    Guid ProfileId,
    Guid? TenantId,
    Guid? WorkspaceId,
    Guid? ProjectId,
    Guid? KnowledgeScopeId,
    Guid AthanorProjectId,
    string AgentName,
    string Objective,
    string TraceId,
    AgentRunStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string? ErrorMessage,
    IReadOnlyList<AgentStepDto> Steps,
    IReadOnlyList<TraceEventDto> Trace,
    IReadOnlyList<HumanReviewDecisionDto> HumanReviewDecisions);

public sealed record AgentRunSummaryDto(
    Guid Id,
    Guid ProfileId,
    Guid? TenantId,
    Guid? WorkspaceId,
    Guid? ProjectId,
    Guid? KnowledgeScopeId,
    string AgentName,
    string TraceId,
    AgentRunStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt);

public sealed record AgentRunListResponseDto(
    IReadOnlyList<AgentRunSummaryDto> Items,
    int TotalCount,
    int Skip,
    int Take);
