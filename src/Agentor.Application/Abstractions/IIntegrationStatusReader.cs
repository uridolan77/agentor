using Agentor.Contracts;

namespace Agentor.Application.Abstractions;

/// <summary>Read-only integration surface for operator dashboards (Phase 22 PR108).</summary>
public interface IIntegrationStatusReader
{
    Task<IntegrationsStatusResponseDto> GetStatusAsync(CancellationToken cancellationToken = default);
}
