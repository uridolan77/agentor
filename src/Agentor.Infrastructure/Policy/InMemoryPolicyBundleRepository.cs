using System.Collections.Concurrent;
using Agentor.Application.Abstractions;
using Agentor.Domain.Policy;

namespace Agentor.Infrastructure.Policy;

public sealed class InMemoryPolicyBundleRepository : IPolicyBundleRepository
{
    private readonly ConcurrentDictionary<Guid, PolicyBundle> _store = new();

    public Task SaveAsync(PolicyBundle bundle, CancellationToken cancellationToken)
    {
        _store[bundle.Id] = bundle;
        return Task.CompletedTask;
    }

    public Task<PolicyBundle?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_store.TryGetValue(id, out var b) ? b : null);

    public Task<IReadOnlyList<PolicyBundle>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PolicyBundle>>(
            [.. _store.Values.OrderBy(b => b.CreatedAt)]);
}
