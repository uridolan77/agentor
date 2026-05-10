using System.Collections.Concurrent;
using Agentor.Application.Abstractions;
using Agentor.Infrastructure.Persistence.Records;

namespace Agentor.Infrastructure.Persistence;

public sealed class InMemoryExecutionLeaseStore : IRunExecutionLeaseStore
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, ExecutionLeaseRecord> _leases = new();

    public Task<LeaseAcquireOutcome> TryAcquireAsync(
        Guid resourceId,
        string leaseHolder,
        TimeSpan ttl,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_leases.TryGetValue(resourceId, out var existing))
            {
                if (existing.ExpiresAtUtc > now)
                {
                    if (string.Equals(existing.LeaseHolder, leaseHolder, StringComparison.Ordinal))
                    {
                        return Task.FromResult(LeaseAcquireOutcome.AlreadyHeldByCaller);
                    }

                    return Task.FromResult(LeaseAcquireOutcome.Contested);
                }
            }

            _leases[resourceId] = new ExecutionLeaseRecord
            {
                ResourceId = resourceId,
                LeaseHolder = leaseHolder,
                ExpiresAtUtc = now.Add(ttl),
                CreatedAtUtc = now,
            };

            return Task.FromResult(LeaseAcquireOutcome.Acquired);
        }
    }

    public Task<bool> TryRenewAsync(
        Guid resourceId,
        string leaseHolder,
        TimeSpan extendBy,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (!_leases.TryGetValue(resourceId, out var row))
            {
                return Task.FromResult(false);
            }

            if (!string.Equals(row.LeaseHolder, leaseHolder, StringComparison.Ordinal))
            {
                return Task.FromResult(false);
            }

            if (row.ExpiresAtUtc <= now)
            {
                return Task.FromResult(false);
            }

            row.ExpiresAtUtc = now.Add(extendBy);
            return Task.FromResult(true);
        }
    }

    public Task ReleaseAsync(Guid resourceId, string leaseHolder, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_leases.TryGetValue(resourceId, out var row)
                && string.Equals(row.LeaseHolder, leaseHolder, StringComparison.Ordinal))
            {
                _leases.Remove(resourceId);
            }
        }

        return Task.CompletedTask;
    }
}
