using Agentor.Application.Abstractions;
using Agentor.Application.Reliability;
using Agentor.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;

namespace Agentor.Infrastructure.Persistence;

public sealed class EfOutboxStore : IOutboxStore
{
    private readonly AgentorDbContext _db;

    public EfOutboxStore(AgentorDbContext db)
    {
        _db = db;
    }

    public async Task AppendAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        _db.OutboxMessages.Add(ToRecord(message));
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<OutboxMessage>> ListPendingForDispatchAsync(int take, CancellationToken cancellationToken)
    {
        var pending = OutboxStatus.Pending.ToString();
        var rows = await _db.OutboxMessages.AsNoTracking()
            .Where(r => r.Status == pending)
            .OrderBy(r => r.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(FromRecord).ToList();
    }

    public async Task<bool> TryMarkDispatchingAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await _db.OutboxMessages.FirstOrDefaultAsync(r => r.Id == id, cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            return false;
        }

        if (row.Status != OutboxStatus.Pending.ToString())
        {
            return false;
        }

        row.Status = OutboxStatus.Dispatching.ToString();
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task MarkOutcomeAsync(Guid id, OutboxStatus status, string? detail, CancellationToken cancellationToken)
    {
        var row = await _db.OutboxMessages.FirstOrDefaultAsync(r => r.Id == id, cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            return;
        }

        row.Status = status.ToString();
        if (detail is not null)
        {
            row.LastError = detail;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task IncrementAttemptAndRequeueOrPoisonAsync(Guid id, string error, int maxAttempts, CancellationToken cancellationToken)
    {
        var row = await _db.OutboxMessages.FirstOrDefaultAsync(r => r.Id == id, cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            return;
        }

        row.AttemptCount++;
        row.LastError = error;
        if (row.AttemptCount >= maxAttempts)
        {
            row.Status = OutboxStatus.Poison.ToString();
        }
        else
        {
            row.Status = OutboxStatus.Pending.ToString();
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static OutboxMessageRecord ToRecord(OutboxMessage m) => new()
    {
        Id = m.Id,
        Kind = m.Kind.ToString(),
        PayloadJson = m.PayloadJson,
        Status = m.Status.ToString(),
        AttemptCount = m.AttemptCount,
        CreatedAt = m.CreatedAt,
        LastError = m.LastError,
    };

    private static OutboxMessage FromRecord(OutboxMessageRecord r) => new(
        r.Id,
        Enum.Parse<OutboxMessageKind>(r.Kind),
        r.PayloadJson,
        Enum.Parse<OutboxStatus>(r.Status),
        r.AttemptCount,
        r.CreatedAt,
        r.LastError);
}
