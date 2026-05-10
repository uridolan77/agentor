using Agentor.Application.Abstractions;
using Agentor.Contracts.ExternalAgents;

namespace Agentor.Infrastructure.ExternalAgents;

public sealed class DisabledExternalAgentProtocolClient : IExternalAgentProtocolClient
{
    private static readonly InvalidOperationException Error = new(
        "External agent integration is disabled (configure Agentor:Integrations:ExternalAgents:Mode).");

    public Task<IReadOnlyList<ExternalAgentCapabilityDto>> ListCapabilitiesAsync(
        ExternalAgentProtocolKind protocolKind,
        CancellationToken cancellationToken = default) =>
        Task.FromException<IReadOnlyList<ExternalAgentCapabilityDto>>(Error);

    public Task<ExternalAgentInvocationResultDto> InvokeAsync(
        ExternalAgentInvocationRequestDto request,
        CancellationToken cancellationToken = default) =>
        Task.FromException<ExternalAgentInvocationResultDto>(Error);
}
