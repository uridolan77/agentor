using Agentor.Application.Abstractions;
using Agentor.Contracts;

namespace Agentor.Infrastructure.IntegrationStatus;

public sealed class IntegrationStatusReader(IntegrationSurfaceService inner) : IIntegrationStatusReader
{
    public Task<IntegrationsStatusResponseDto> GetStatusAsync(CancellationToken cancellationToken = default) =>
        inner.GetStatusAsync(cancellationToken);
}
