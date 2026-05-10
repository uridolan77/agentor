using Agentor.Application.Abstractions;
using Agentor.Domain.Policy;

namespace Agentor.Infrastructure.Policy;

public sealed class InMemoryPolicyProfileRepository : IPolicyProfileRepository
{
    private volatile ActivePolicyProfile? _active;

    public Task<ActivePolicyProfile?> GetActiveAsync(CancellationToken cancellationToken) =>
        Task.FromResult(_active);

    public Task SetActiveAsync(ActivePolicyProfile active, CancellationToken cancellationToken)
    {
        _active = active;
        return Task.CompletedTask;
    }
}
