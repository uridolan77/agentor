using System.Collections.Concurrent;
using Agentor.Application.Abstractions;
using Agentor.Domain;

namespace Agentor.Infrastructure;

public sealed class InMemoryAgentRunRepository : IAgentRunRepository
{
    private readonly ConcurrentDictionary<Guid, AgentRun> _runs = new();

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
}
