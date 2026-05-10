namespace Agentor.Infrastructure.Persistence.Records;

public sealed class AgentRunRecord
{
    public Guid Id { get; set; }
    public Guid ProfileId { get; set; }
    public Guid? TenantId { get; set; }
    public Guid? WorkspaceId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? KnowledgeScopeId { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public string TraceId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }

    public string SessionMemoryJson { get; set; } = "{}";

    public string HumanReviewDecisionsJson { get; set; } = "[]";

    public List<AgentStepRecord> Steps { get; set; } = [];
    public List<TraceEventRecord> TraceEvents { get; set; } = [];
}
