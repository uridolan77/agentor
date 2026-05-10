using Agentor.Application.Abstractions;
using Agentor.Domain;
using Agentor.Domain.Enums;
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

    public async Task<AgentRunListPage> ListSummariesAsync(
        int skip,
        int take,
        CancellationToken cancellationToken,
        AgentRunStatus? statusFilter = null)
    {
        var query = _context.AgentRuns.AsNoTracking();
        if (statusFilter is not null)
        {
            var statusString = statusFilter.Value.ToString();
            query = query.Where(r => r.Status == statusString);
        }

        query = query
            .OrderByDescending(r => r.StartedAt)
            .ThenBy(r => r.Id);

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        var items = rows.Select(RecordMapper.ToSummary).ToList();
        return new AgentRunListPage(items, total, skip, take);
    }
}
