namespace Agentor.Infrastructure.Persistence.Records;

public sealed class ExecutionLeaseRecord
{
    public Guid ResourceId { get; set; }

    public string LeaseHolder { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
