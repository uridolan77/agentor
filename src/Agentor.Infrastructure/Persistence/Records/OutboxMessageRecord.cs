namespace Agentor.Infrastructure.Persistence.Records;

public sealed class OutboxMessageRecord
{
    public Guid Id { get; set; }

    public string Kind { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = "{}";

    public string Status { get; set; } = string.Empty;

    public int AttemptCount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string? LastError { get; set; }
}
