using Microsoft.Extensions.Logging;

namespace Agentor.Application.Observability;

/// <summary>
/// Stable structured-log event ids (Phase 37 — observability primitives).
/// </summary>
public static class AgentorEventIds
{
    public static readonly EventId RunStarted = new(10_001, "run.started");
    public static readonly EventId RunCompleted = new(10_002, "run.completed");
    public static readonly EventId RunFailed = new(10_003, "run.failed");
    public static readonly EventId RunRequiresReview = new(10_004, "run.requires_review");

    public static readonly EventId PolicyAllowed = new(10_101, "policy.allowed");
    public static readonly EventId PolicyDenied = new(10_102, "policy.denied");
    public static readonly EventId PolicyRequiresReview = new(10_103, "policy.requires_review");

    public static readonly EventId ToolStarted = new(10_201, "tool.started");
    public static readonly EventId ToolCompleted = new(10_202, "tool.completed");
    public static readonly EventId ToolFailed = new(10_203, "tool.failed");

    public static readonly EventId QueueClaimed = new(10_301, "queue.claimed");
    public static readonly EventId QueueCompleted = new(10_302, "queue.completed");
    public static readonly EventId QueueFailed = new(10_303, "queue.failed");

    public static readonly EventId OutboxDispatchStarted = new(10_401, "outbox.dispatch.started");
    public static readonly EventId OutboxDispatchCompleted = new(10_402, "outbox.dispatch.completed");
    public static readonly EventId OutboxDispatchFailed = new(10_403, "outbox.dispatch.failed");

    public static readonly EventId IntegrationError = new(10_501, "integration.error");
}
