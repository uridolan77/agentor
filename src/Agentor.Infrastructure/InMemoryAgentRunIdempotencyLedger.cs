using System.Collections.Concurrent;
using Agentor.Application.Abstractions;

namespace Agentor.Infrastructure;

public sealed class InMemoryAgentRunIdempotencyLedger : IAgentRunIdempotencyLedger
{
    private readonly ConcurrentDictionary<string, AgentRunIdempotencyEntry> _entries = new(StringComparer.Ordinal);

    public Task<AgentRunIdempotencyEntry?> GetAsync(string idempotencyKey, CancellationToken cancellationToken)
    {
        _entries.TryGetValue(idempotencyKey, out var entry);
        return Task.FromResult(entry);
    }

    public Task SaveAsync(
        string idempotencyKey,
        string requestFingerprint,
        Guid agentRunId,
        CancellationToken cancellationToken)
    {
        _entries[idempotencyKey] = new AgentRunIdempotencyEntry(requestFingerprint, agentRunId);
        return Task.CompletedTask;
    }
}