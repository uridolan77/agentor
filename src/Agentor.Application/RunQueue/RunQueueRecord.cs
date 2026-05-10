using Agentor.Application.Commands;

namespace Agentor.Application.RunQueue;

public enum DurableRunQueueStatus
{
    Pending,
    Claimed,
    Completed,
    Failed,
}

public sealed record RunQueueRecord(
    Guid WorkItemId,
    StartAgentRunCommand Command,
    DurableRunQueueStatus Status,
    DateTimeOffset EnqueuedAtUtc,
    string? ClaimedBy,
    DateTimeOffset? LeaseExpiresAtUtc,
    Guid? AgentRunId,
    string? Error);
