using Agentor.Domain.Policy;

namespace Agentor.Application.Abstractions;

public interface IPolicyProfileRepository
{
    Task<ActivePolicyProfile?> GetActiveAsync(CancellationToken cancellationToken);
    Task SetActiveAsync(ActivePolicyProfile active, CancellationToken cancellationToken);
}
