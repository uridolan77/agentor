using Agentor.Domain.Enums;

namespace Agentor.Contracts;

public sealed record RunManifestDto(
    Guid RunId,
    Guid ProfileId,
    string TraceId,
    AgentRunStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    int StepCount,
    int ToolCallCount,
    int PolicyDecisionCount,
    int TraceEventCount,
    int ModelCallCount,
    long TotalModelPromptTokens,
    long TotalModelCompletionTokens,
    decimal TotalModelEstimatedCostUnits,
    long TotalModelLatencyMs,
    string? PrimaryModelProviderName,
    string? PrimaryModelId,
    string? PrimaryPromptProfileRef,
    string? PrimaryModelProfileRef,
    string ManifestVersion,
    string ContentHash);
