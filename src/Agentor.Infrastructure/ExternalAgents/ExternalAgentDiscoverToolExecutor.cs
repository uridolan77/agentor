using System.Globalization;
using Agentor.Application;
using Agentor.Application.Abstractions;
using Agentor.Contracts.ExternalAgents;
using Agentor.Domain;

namespace Agentor.Infrastructure.ExternalAgents;

public sealed class ExternalAgentDiscoverToolExecutor : IToolExecutor
{
    private readonly IExternalAgentProtocolClient _client;

    public ExternalAgentDiscoverToolExecutor(IExternalAgentProtocolClient client)
    {
        _client = client;
    }

    public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request, CancellationToken cancellationToken)
    {
        var kind = ParseProtocolKind(request.Input.ToPolicyEvaluationDictionary());
        var caps = await _client.ListCapabilitiesAsync(kind, cancellationToken).ConfigureAwait(false);

        var output = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["protocolKind"] = kind.ToString(),
            ["capabilityCount"] = caps.Count.ToString(CultureInfo.InvariantCulture),
            ["toolKey"] = request.ToolKey,
            ["nonCanon"] = "true",
        };

        for (var i = 0; i < caps.Count; i++)
        {
            var c = caps[i];
            output[$"capability.{i}.protocolKind"] = c.ProtocolKind.ToString();
            output[$"capability.{i}.agentKey"] = c.AgentKey;
            output[$"capability.{i}.capabilityKey"] = c.CapabilityKey;
            output[$"capability.{i}.summary"] = c.Summary;
        }

        return new ToolExecutionResult(true, ToolPayload.FromLegacyDictionary(output));
    }

    private static ExternalAgentProtocolKind ParseProtocolKind(IReadOnlyDictionary<string, string> input)
    {
        if (!input.TryGetValue("protocolKind", out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return ExternalAgentProtocolKind.Unspecified;
        }

        return Enum.TryParse<ExternalAgentProtocolKind>(raw.Trim(), ignoreCase: true, out var k)
            ? k
            : ExternalAgentProtocolKind.Unspecified;
    }
}
