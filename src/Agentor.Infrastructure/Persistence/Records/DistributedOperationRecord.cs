namespace Agentor.Infrastructure.Persistence.Records;

public sealed class DistributedOperationRecord
{
    public string OperationKey { get; set; } = string.Empty;

    public DateTimeOffset CommittedAtUtc { get; set; }
}
