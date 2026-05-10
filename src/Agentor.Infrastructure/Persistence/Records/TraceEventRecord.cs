namespace Agentor.Infrastructure.Persistence.Records;

public sealed class TraceEventRecord
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
    public string DataJson { get; set; } = "{}";

    public AgentRunRecord Run { get; set; } = null!;
}
