namespace Agentor.Infrastructure.Persistence.Records;

public sealed class AgentStepRecord
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public AgentRunRecord Run { get; set; } = null!;
    public List<ToolCallRecord> ToolCalls { get; set; } = [];
    public List<PolicyDecisionRecord> PolicyDecisions { get; set; } = [];
}
