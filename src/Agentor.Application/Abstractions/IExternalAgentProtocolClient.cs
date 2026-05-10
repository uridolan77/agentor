using Agentor.Contracts.ExternalAgents;

namespace Agentor.Application.Abstractions;

public interface IExternalAgentProtocolClient
{
    Task<IReadOnlyList<ExternalAgentCapabilityDto>> ListCapabilitiesAsync(
        ExternalAgentProtocolKind protocolKind,
        CancellationToken cancellationToken = default);

    Task<ExternalAgentInvocationResultDto> InvokeAsync(
        ExternalAgentInvocationRequestDto request,
        CancellationToken cancellationToken = default);
}
