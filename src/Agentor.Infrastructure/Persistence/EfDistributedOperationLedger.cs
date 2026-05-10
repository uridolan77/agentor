using Agentor.Application.Abstractions;
using Agentor.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;

namespace Agentor.Infrastructure.Persistence;

public sealed class EfDistributedOperationLedger : IDistributedOperationLedger
{
    private readonly AgentorDbContext _db;

    public EfDistributedOperationLedger(AgentorDbContext db)
    {
        _db = db;
    }

    public async Task<bool> TryCommitOnceAsync(string operationKey, CancellationToken cancellationToken)
    {
        _db.DistributedOperations.Add(new DistributedOperationRecord
        {
            OperationKey = operationKey,
            CommittedAtUtc = DateTimeOffset.UtcNow,
        });

        try
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _db.ChangeTracker.Clear();
            return true;
        }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();
            return false;
        }
    }
}
