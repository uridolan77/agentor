import pathlib
ROOT = pathlib.Path(r"c:/dev/agentor")
ROOT.joinpath("write_phase9_payload.py").write_text("""
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
""", encoding="utf-8")
exec(ROOT.joinpath("write_phase9_payload.py").read_text(encoding="utf-8"))
