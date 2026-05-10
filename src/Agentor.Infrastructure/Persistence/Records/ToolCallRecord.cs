namespace Agentor.Infrastructure.Persistence.Records;

public sealed class ToolCallRecord
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public Guid StepId { get; set; }
    public string ToolKey { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string InputJson { get; set; } = "{}";
    public string OutputJson { get; set; } = "{}";
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }

    public AgentStepRecord Step { get; set; } = null!;
}
