import pathlib
ROOT = pathlib.Path(r"c:/dev/agentor")
FILES = {}
FILES["src/Agentor.Contracts/ExternalAgents/A2AExternalAgentDtos.cs"] = r'''namespace Agentor.Contracts.ExternalAgents;

/// <summary>A2A-shaped presentation DTOs only (not Agentor domain).</summary>
public sealed record A2ACapabilityDto(string Id, string Title, string Summary);

public sealed record A2AAgentCardDto(
    string AgentKey,
    string DisplayName,
    string Summary,
    IReadOnlyList<A2ACapabilityDto> Capabilities);

public sealed record A2AInvocationMetadataDto(
    string AgentKey,
    string CapabilityId,
    string RequestFingerprint);
'''
FILES["src/Agentor.Infrastructure/ExternalAgents/FakeA2AExternalAgentClient.cs"] = r'''using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Agentor.Application.Abstractions;
using Agentor.Contracts.ExternalAgents;

namespace Agentor.Infrastructure.ExternalAgents;

/// <summary>
/// Deterministic fake modeling agent-card discovery and invocation (no conformance claim; no network).
/// </summary>
public sealed class FakeA2AExternalAgentClient : IExternalAgentProtocolClient
{
    private static readonly IReadOnlyList<A2AAgentCardDto> Cards =
    [
        new A2AAgentCardDto(
            "alpha-agent",
            "Alpha (fake A2A-style)",
            "Deterministic demo agent card.",
            [
                new A2ACapabilityDto("reply", "Reply", "Echo-shaped reply."),
                new A2ACapabilityDto("transform", "Transform", "Deterministic transform."),
            ]),
    ];

    public Task<IReadOnlyList<ExternalAgentCapabilityDto>> ListCapabilitiesAsync(
        ExternalAgentProtocolKind protocolKind,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var kind = protocolKind == ExternalAgentProtocolKind.Unspecified
            ? ExternalAgentProtocolKind.A2AStyled
            : protocolKind;

        if (kind != ExternalAgentProtocolKind.A2AStyled)
        {
            return Task.FromResult<IReadOnlyList<ExternalAgentCapabilityDto>>(Array.Empty<ExternalAgentCapabilityDto>());
        }

        var rows = new List<ExternalAgentCapabilityDto>();
        foreach (var card in Cards)
        {
            foreach (var cap in card.Capabilities)
            {
                rows.Add(new ExternalAgentCapabilityDto(
                    ExternalAgentProtocolKind.A2AStyled,
                    card.AgentKey,
                    cap.Id,
                    $"{card.DisplayName}: {cap.Title}"));
            }
        }

        return Task.FromResult<IReadOnlyList<ExternalAgentCapabilityDto>>(rows);
    }

    public Task<ExternalAgentInvocationResultDto> InvokeAsync(
        ExternalAgentInvocationRequestDto request,
        CancellationToken cancellationToken = default)
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
        var fingerprint = BuildFingerprint(request.AgentKey.Trim(), request.CapabilityKey.Trim(), sorted);
        var meta = new A2AInvocationMetadataDto(request.AgentKey.Trim(), request.CapabilityKey.Trim(), fingerprint);

        var payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["protocolKind"] = ExternalAgentProtocolKind.A2AStyled.ToString(),
            ["agentKey"] = meta.AgentKey,
            ["capabilityKey"] = meta.CapabilityId,
            ["requestFingerprint"] = meta.RequestFingerprint,
            ["artifact"] = $"fake-a2a:{meta.RequestFingerprint}",
            ["nonCanon"] = "true",
            ["invocationStatus"] = ExternalAgentInvocationStatus.Succeeded.ToString(),
        };

        return Task.FromResult(new ExternalAgentInvocationResultDto(
            ExternalAgentInvocationStatus.Succeeded,
            payload,
            null,
            IsNonCanonEvidence: true));
    }

    private static string BuildFingerprint(string agentKey, string capabilityKey, KeyValuePair<string, string>[] sortedInput)
    {
        var canonical =
            $"{agentKey}:{capabilityKey}:{string.Join(";", sortedInput.Select(kv => $"{kv.Key}={kv.Value}"))}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(hash)[..24].ToLowerInvariant();
    }
}
'''
FILES["tests/Agentor.Infrastructure.Tests/FakeA2AExternalAgentClientTests.cs"] = r'''using Agentor.Contracts.ExternalAgents;
using Agentor.Infrastructure.ExternalAgents;
using Xunit;

namespace Agentor.Infrastructure.Tests;

public sealed class FakeA2AExternalAgentClientTests
{
    private readonly FakeA2AExternalAgentClient _sut = new();

    [Fact]
    public async Task ListCapabilities_ReturnsRowsPerCapability()
    {
        var caps = await _sut.ListCapabilitiesAsync(ExternalAgentProtocolKind.A2AStyled);
        Assert.Equal(2, caps.Count);
        Assert.All(caps, c => Assert.Equal(ExternalAgentProtocolKind.A2AStyled, c.ProtocolKind));
    }

    [Fact]
    public async Task Invoke_IsDeterministic()
    {
        var req = new ExternalAgentInvocationRequestDto(
            ExternalAgentProtocolKind.A2AStyled,
            "alpha-agent",
            "reply",
            new Dictionary<string, string> { ["x"] = "1" });

        var a = await _sut.InvokeAsync(req);
        var b = await _sut.InvokeAsync(req);

        Assert.Equal(a.OutputPayload!["artifact"], b.OutputPayload!["artifact"]);
    }
}
'''
for rel, content in FILES.items():
    p = ROOT / rel
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(content, encoding="utf-8")
print("pr42", len(FILES))
