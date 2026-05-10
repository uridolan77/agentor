using Agentor.Domain.Enums;
using Agentor.Domain.Governance;

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
    DateTimeOffset? TerminalAt,
    DateTimeOffset? ReviewRequestedAt,
    DateTimeOffset? PausedAt,
    HumanReviewWorkflowStatus ReviewWorkflowStatus,
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
    DateTimeOffset? CompletedAt,
    DateTimeOffset? TerminalAt,
    DateTimeOffset? ReviewRequestedAt,
    DateTimeOffset? PausedAt,
    HumanReviewWorkflowStatus ReviewWorkflowStatus,
    string? ErrorMessage = null);

public sealed record AgentRunListResponseDto(
    IReadOnlyList<AgentRunSummaryDto> Items,
    int TotalCount,
    int Skip,
    int Take);
