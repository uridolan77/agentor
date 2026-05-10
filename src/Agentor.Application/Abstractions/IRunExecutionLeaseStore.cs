namespace Agentor.Application.Abstractions;

public enum LeaseAcquireOutcome
{
    Acquired,
    AlreadyHeldByCaller,
    Contested,
}

public sealed record ExecutionLeaseSnapshot(
    Guid ResourceId,
    string LeaseHolder,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset CreatedAtUtc);

public interface IRunExecutionLeaseStore
{
    Task<LeaseAcquireOutcome> TryAcquireAsync(
        Guid resourceId,
        string leaseHolder,
        TimeSpan ttl,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<bool> TryRenewAsync(
        Guid resourceId,
        string leaseHolder,
        TimeSpan extendBy,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task ReleaseAsync(Guid resourceId, string leaseHolder, CancellationToken cancellationToken);

    Task<IReadOnlyList<ExecutionLeaseSnapshot>> ListLeasesAsync(int take, CancellationToken cancellationToken);
}
