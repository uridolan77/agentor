import pathlib
ROOT = pathlib.Path(r"c:/dev/agentor")
FILES = {}
FILES["src/Agentor.Infrastructure/ExternalAgents/FakeExternalAgentProtocolClient.cs"] = r'''using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Agentor.Application.Abstractions;
using Agentor.Contracts.ExternalAgents;

namespace Agentor.Infrastructure.ExternalAgents;

public sealed class FakeExternalAgentProtocolClient : IExternalAgentProtocolClient
{
    public Task<IReadOnlyList<ExternalAgentCapabilityDto>> ListCapabilitiesAsync(
        ExternalAgentProtocolKind protocolKind,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var kind = protocolKind == ExternalAgentProtocolKind.Unspecified
            ? ExternalAgentProtocolKind.GenericFake
            : protocolKind;

        if (kind != ExternalAgentProtocolKind.GenericFake)
        {
            return Task.FromResult<IReadOnlyList<ExternalAgentCapabilityDto>>(Array.Empty<ExternalAgentCapabilityDto>());
        }

        IReadOnlyList<ExternalAgentCapabilityDto> caps =
        [
            new ExternalAgentCapabilityDto(kind, "demo-agent", "echo", "Echo inputs deterministically."),
            new ExternalAgentCapabilityDto(kind, "demo-agent", "summarize", "Summarize inputs deterministically."),
        ];

        return Task.FromResult(caps);
    }

    public Task<ExternalAgentInvocationResultDto> InvokeAsync(
        ExternalAgentInvocationRequestDto request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.AgentKey) || string.IsNullOrWhiteSpace(request.CapabilityKey))
        {
            return Task.FromResult(new ExternalAgentInvocationResultDto(
                ExternalAgentInvocationStatus.Failed,
                null,
                "agentKey and capabilityKey are required.",
                IsNonCanonEvidence: true));
        }

        var sorted = request.Input.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToArray();
        var artifactSeed = BuildArtifactSeed(request.AgentKey.Trim(), request.CapabilityKey.Trim(), sorted);

        var payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["protocolKind"] = request.ProtocolKind.ToString(),
            ["agentKey"] = request.AgentKey.Trim(),
            ["capabilityKey"] = request.CapabilityKey.Trim(),
            ["echo"] = string.Join("|", sorted.Select(kv => $"{kv.Key}={kv.Value}")),
            ["artifact"] = $"fake-external:{artifactSeed}",
            ["nonCanon"] = "true",
            ["invocationStatus"] = ExternalAgentInvocationStatus.Succeeded.ToString(),
        };

        return Task.FromResult(new ExternalAgentInvocationResultDto(
            ExternalAgentInvocationStatus.Succeeded,
            payload,
            null,
            IsNonCanonEvidence: true));
    }

    private static string BuildArtifactSeed(string agentKey, string capabilityKey, KeyValuePair<string, string>[] sortedInput)
    {
        var canonical =
            $"{agentKey}:{capabilityKey}:{string.Join(";", sortedInput.Select(kv => $"{kv.Key}={kv.Value}"))}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }
}
'''
FILES["tests/Agentor.Infrastructure.Tests/FakeExternalAgentProtocolClientTests.cs"] = r'''using Agentor.Contracts.ExternalAgents;
using Agentor.Infrastructure.ExternalAgents;
using Xunit;

namespace Agentor.Infrastructure.Tests;

public sealed class FakeExternalAgentProtocolClientTests
{
    private readonly FakeExternalAgentProtocolClient _sut = new();

    [Fact]
    public async Task ListCapabilities_GenericFake_ReturnsDeterministicRows()
    {
        var caps = await _sut.ListCapabilitiesAsync(ExternalAgentProtocolKind.GenericFake);
        Assert.Equal(2, caps.Count);
        Assert.All(caps, c => Assert.Equal(ExternalAgentProtocolKind.GenericFake, c.ProtocolKind));
        Assert.Contains(caps, c => c.AgentKey == "demo-agent" && c.CapabilityKey == "echo");
    }

    [Fact]
    public async Task ListCapabilities_NonGeneric_ReturnsEmpty()
    {
        var caps = await _sut.ListCapabilitiesAsync(ExternalAgentProtocolKind.A2AStyled);
        Assert.Empty(caps);
    }

    [Fact]
    public async Task Invoke_ProducesDeterministicArtifact_ForSameInputs()
    {
        var input = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["a"] = "1",
            ["b"] = "2",
        };

        var req = new ExternalAgentInvocationRequestDto(
            ExternalAgentProtocolKind.GenericFake,
            "demo-agent",
            "echo",
            input);

        var r1 = await _sut.InvokeAsync(req);
        var r2 = await _sut.InvokeAsync(req);

        Assert.Equal(ExternalAgentInvocationStatus.Succeeded, r1.Status);
        Assert.NotNull(r1.OutputPayload);
        Assert.True(r1.IsNonCanonEvidence);
        Assert.Equal(r1.OutputPayload!["artifact"], r2.OutputPayload!["artifact"]);
    }

    [Fact]
    public async Task Invoke_MissingKeys_Fails()
    {
        var req = new ExternalAgentInvocationRequestDto(
            ExternalAgentProtocolKind.GenericFake,
            " ",
            "echo",
            new Dictionary<string, string>());

        var r = await _sut.InvokeAsync(req);
        Assert.Equal(ExternalAgentInvocationStatus.Failed, r.Status);
        Assert.Null(r.OutputPayload);
    }
}
'''
FILES["docs/EXTERNAL_AGENT_PROTOCOL_BOUNDARY.md"] = r'''# External agent protocol boundary

## Role

External-agent protocols (including A2A-shaped transports in future phases) are **adapters** behind `IExternalAgentProtocolClient`. They are not part of Agentor domain ontology.

## Rules

- Domain and Application **must not** reference protocol SDK types, HTTP clients, or framework-specific agent graphs.
- Contract DTOs under `Agentor.Contracts.ExternalAgents` describe portable payloads only.
- Outputs from external agents are **non-canon evidence** until reviewed in Athanor or another authority outside Agentor.
- Phase 9 ships **fake, deterministic** implementations only (no real network transport).

## Port

`IExternalAgentProtocolClient` (`Agentor.Application.Abstractions`):

- `ListCapabilitiesAsync` — discovery-shaped capability rows for policy-gated tooling.
- `InvokeAsync` — invocation-shaped call with dictionary inputs matching other Agentor tools.

Concrete adapters live in Infrastructure and register through dependency injection.
'''
for rel, content in FILES.items():
    p = ROOT / rel
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(content, encoding="utf-8")
print("written", len(FILES))
