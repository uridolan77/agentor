
FILES = {
    "src/Agentor.Application/Abstractions/IExternalAgentProtocolClient.cs": r'''using Agentor.Contracts.ExternalAgents;

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
''',
}
for rel, content in FILES.items():
    p = ROOT / rel
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(content, encoding="utf-8")
print("ok")
