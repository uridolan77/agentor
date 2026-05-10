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

    /// <summary>Optional explicit execution mode name persisted for queue replay.</summary>
    public string? ExecutionMode { get; set; }

    public Guid? RecipeId { get; set; }

    public Guid? PlanId { get; set; }

    public string? ToolKey { get; set; }

    public string? SkillKey { get; set; }

    /// <summary>JSON object of string key/value pairs for tool input.</summary>
    public string? ToolInputJson { get; set; }
}
