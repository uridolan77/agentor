using Agentor.Domain.Policy;

namespace Agentor.Application.Abstractions;

public interface IPolicyBundleRepository
{
    Task SaveAsync(PolicyBundle bundle, CancellationToken cancellationToken);
    Task<PolicyBundle?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<PolicyBundle>> ListAsync(CancellationToken cancellationToken);
}
