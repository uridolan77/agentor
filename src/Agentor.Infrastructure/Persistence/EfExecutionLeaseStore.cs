using Agentor.Application.Abstractions;
using Agentor.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;

namespace Agentor.Infrastructure.Persistence;

public sealed class EfExecutionLeaseStore : IRunExecutionLeaseStore
{
    private readonly AgentorDbContext _db;

    public EfExecutionLeaseStore(AgentorDbContext db)
    {
        _db = db;
    }

    public async Task<LeaseAcquireOutcome> TryAcquireAsync(
        Guid resourceId,
        string leaseHolder,
        TimeSpan ttl,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var row = await _db.ExecutionLeases.FirstOrDefaultAsync(r => r.ResourceId == resourceId, cancellationToken).ConfigureAwait(false);
        if (row is not null && row.ExpiresAtUtc > now)
        {
            if (string.Equals(row.LeaseHolder, leaseHolder, StringComparison.Ordinal))
            {
                await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
                return LeaseAcquireOutcome.AlreadyHeldByCaller;
            }

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return LeaseAcquireOutcome.Contested;
        }

        if (row is not null)
        {
            _db.ExecutionLeases.Remove(row);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        _db.ExecutionLeases.Add(new ExecutionLeaseRecord
        {
            ResourceId = resourceId,
            LeaseHolder = leaseHolder,
            ExpiresAtUtc = now.Add(ttl),
            CreatedAtUtc = now,
        });

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return LeaseAcquireOutcome.Acquired;
    }

    public async Task<bool> TryRenewAsync(
        Guid resourceId,
        string leaseHolder,
        TimeSpan extendBy,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var row = await _db.ExecutionLeases.FirstOrDefaultAsync(r => r.ResourceId == resourceId, cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            return false;
        }

        if (!string.Equals(row.LeaseHolder, leaseHolder, StringComparison.Ordinal))
        {
            return false;
        }

        if (row.ExpiresAtUtc <= now)
        {
            return false;
        }

        row.ExpiresAtUtc = now.Add(extendBy);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task ReleaseAsync(Guid resourceId, string leaseHolder, CancellationToken cancellationToken)
    {
        var row = await _db.ExecutionLeases.FirstOrDefaultAsync(
                r => r.ResourceId == resourceId && r.LeaseHolder == leaseHolder,
                cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            return;
        }

        _db.ExecutionLeases.Remove(row);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
