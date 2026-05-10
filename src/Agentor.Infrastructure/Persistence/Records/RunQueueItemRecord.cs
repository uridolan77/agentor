namespace Agentor.Infrastructure.Persistence.Records;

public sealed class RunQueueItemRecord
{
    public Guid WorkItemId { get; set; }

    public string AgentName { get; set; } = string.Empty;

    public string Objective { get; set; } = string.Empty;

    public string? TraceId { get; set; }

    public Guid? TenantId { get; set; }

    public Guid? WorkspaceId { get; set; }

    public Guid? ProjectId { get; set; }

    public Guid? KnowledgeScopeId { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset EnqueuedAtUtc { get; set; }

    public string? ClaimedBy { get; set; }

    public DateTimeOffset? LeaseExpiresAtUtc { get; set; }

    public Guid? AgentRunId { get; set; }

    public string? Error { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
