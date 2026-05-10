using Agentor.Application.Abstractions;
using Agentor.Domain;
using Microsoft.EntityFrameworkCore;

namespace Agentor.Infrastructure.Persistence;

public sealed class EfCoreAgentRunRepository : IAgentRunRepository
{
    private readonly AgentorDbContext _context;

    public EfCoreAgentRunRepository(AgentorDbContext context)
    {
        _context = context;
    }

    public async Task SaveAsync(AgentRun run, CancellationToken cancellationToken)
    {
        var existing = await _context.AgentRuns
            .FirstOrDefaultAsync(r => r.Id == run.Id, cancellationToken);

        if (existing is not null)
        {
            _context.AgentRuns.Remove(existing);
            await _context.SaveChangesAsync(cancellationToken);
        }

        var record = RecordMapper.ToRecord(run);
        await _context.AgentRuns.AddAsync(record, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<AgentRun?> GetAsync(Guid runId, CancellationToken cancellationToken)
    {
        var record = await _context.AgentRuns
            .AsNoTracking()
            .Include(r => r.Steps)
                .ThenInclude(s => s.ToolCalls)
            .Include(r => r.Steps)
                .ThenInclude(s => s.PolicyDecisions)
            .Include(r => r.TraceEvents)
            .FirstOrDefaultAsync(r => r.Id == runId, cancellationToken);

        return record is null ? null : RecordMapper.ToDomain(record);
    }
}
