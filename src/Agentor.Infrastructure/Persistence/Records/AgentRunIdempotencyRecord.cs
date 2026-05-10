namespace Agentor.Infrastructure.Persistence.Records;

public sealed class AgentRunIdempotencyRecord
{
    public string IdempotencyKey { get; set; } = string.Empty;

    public string RequestFingerprint { get; set; } = string.Empty;

    public Guid AgentRunId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}