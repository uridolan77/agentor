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
    public DateTimeOffset? TerminalAt { get; set; }
    public DateTimeOffset? ReviewRequestedAt { get; set; }
    public DateTimeOffset? PausedAt { get; set; }
    public string ReviewWorkflowStatus { get; set; } = "None";
    public string? ErrorMessage { get; set; }

    public string SessionMemoryJson { get; set; } = "{}";

    public string HumanReviewDecisionsJson { get; set; } = "[]";

    /// <summary>Serialized <see cref="Agentor.Domain.Governance.PlanResumeCursor"/> when a plan is suspended for review.</summary>
    public string? ResumeCursorJson { get; set; }

    /// <summary>Monotonic optimistic-concurrency version for the aggregate root row.</summary>
    public long AggregateVersion { get; set; }

    public List<AgentStepRecord> Steps { get; set; } = [];
    public List<TraceEventRecord> TraceEvents { get; set; } = [];
}
