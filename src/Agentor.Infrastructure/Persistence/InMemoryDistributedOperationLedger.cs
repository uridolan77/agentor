using System.Collections.Concurrent;
using Agentor.Application.Abstractions;

namespace Agentor.Infrastructure.Persistence;

public sealed class InMemoryDistributedOperationLedger : IDistributedOperationLedger
{
    private readonly ConcurrentDictionary<string, byte> _keys = new(StringComparer.Ordinal);

    public Task<bool> TryCommitOnceAsync(string operationKey, CancellationToken cancellationToken)
    {
        var added = _keys.TryAdd(operationKey, 0);
        return Task.FromResult(added);
    }
}
