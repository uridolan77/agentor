using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Agentor.Application.Abstractions;
using Agentor.Contracts.ExternalAgents;

namespace Agentor.Infrastructure.ExternalAgents;

public sealed class FakeExternalAgentProtocolClient : IExternalAgentProtocolClient
{
    public Task<IReadOnlyList<ExternalAgentCapabilityDto>> ListCapabilitiesAsync(
        ExternalAgentProtocolKind protocolKind,
        CancellationToken cancellationToken = default)
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
