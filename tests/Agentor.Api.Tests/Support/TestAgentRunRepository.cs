using System.Collections.Concurrent;
using Agentor.Application.Abstractions;
using Agentor.Domain;
using Agentor.Domain.Enums;

namespace Agentor.Api.Tests.Support;

/// <summary>
/// In-memory repository for API tests; adds Seed for inserting a run without going through POST /agent-runs (which completes synchronously).
/// </summary>
public sealed class TestAgentRunRepository : IAgentRunRepository
{
    private readonly ConcurrentDictionary<Guid, AgentRun> _runs = new();

    public void Seed(AgentRun run) => _runs[run.Id] = run;

    public Task SaveAsync(AgentRun run, CancellationToken cancellationToken)
    {
        _runs[run.Id] = run;
        return Task.CompletedTask;
    }

    public Task<AgentRun?> GetAsync(Guid runId, CancellationToken cancellationToken)
    {
        _runs.TryGetValue(runId, out var run);
        return Task.FromResult(run);
    }

    public Task<AgentRunListPage> ListSummariesAsync(
        int skip,
        int take,
        CancellationToken cancellationToken,
        AgentRunStatus? statusFilter = null)
    {
        IEnumerable<AgentRun> query = _runs.Values;
        if (statusFilter is not null)
        {
            query = query.Where(r => r.Status == statusFilter);
        }

        var ordered = query
            .OrderByDescending(r => r.StartedAt)
            .ThenBy(r => r.Id)
            .ToList();

        var total = ordered.Count;
        var items = ordered
            .Skip(skip)
            .Take(take)
            .Select(r => new AgentRunSummary(
                r.Id,
                r.ProfileId,
                r.AgentName,
                r.TraceId,
                r.Status,
                r.StartedAt,
                r.CompletedAt,
                r.TenantId,
                r.WorkspaceId,
                r.ProjectId,
                r.KnowledgeScopeId))
            .ToList();

        return Task.FromResult(new AgentRunListPage(items, total, skip, take));
    }
}