using Agentor.Application.Abstractions;
using Agentor.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;

namespace Agentor.Infrastructure.Persistence;

public sealed class EfAgentRunIdempotencyLedger : IAgentRunIdempotencyLedger
{
    private readonly AgentorDbContext _context;

    public EfAgentRunIdempotencyLedger(AgentorDbContext context)
    {
        _context = context;
    }

    public async Task<AgentRunIdempotencyEntry?> GetAsync(string idempotencyKey, CancellationToken cancellationToken)
    {
        var row = await _context.AgentRunIdempotencyKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.IdempotencyKey == idempotencyKey, cancellationToken)
            .ConfigureAwait(false);

        return row is null ? null : new AgentRunIdempotencyEntry(row.RequestFingerprint, row.AgentRunId);
    }

    public async Task SaveAsync(
        string idempotencyKey,
        string requestFingerprint,
        Guid agentRunId,
        CancellationToken cancellationToken)
    {
        await _context.AgentRunIdempotencyKeys.AddAsync(
                new AgentRunIdempotencyRecord
                {
                    IdempotencyKey = idempotencyKey,
                    RequestFingerprint = requestFingerprint,
                    AgentRunId = agentRunId,
                    CreatedAt = DateTimeOffset.UtcNow
                },
                cancellationToken)
            .ConfigureAwait(false);

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}