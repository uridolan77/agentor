using System.Linq;
using Agentor.Application.Abstractions;
using Agentor.Contracts.ExternalAgents;

namespace Agentor.Infrastructure.ExternalAgents;

public sealed class ExternalAgentInvokeToolExecutor : IToolExecutor
{
    private readonly IExternalAgentProtocolClient _client;

    public ExternalAgentInvokeToolExecutor(IExternalAgentProtocolClient client)
    {
        _client = client;
    }

    public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request, CancellationToken cancellationToken)
    {
        if (!request.Input.TryGetValue("agentKey", out var agentKey) || string.IsNullOrWhiteSpace(agentKey))
        {
            return new ToolExecutionResult(false, new Dictionary<string, string>(), "agentKey is required.");
        }

        if (!request.Input.TryGetValue("capabilityKey", out var capabilityKey) || string.IsNullOrWhiteSpace(capabilityKey))
        {
            return new ToolExecutionResult(false, new Dictionary<string, string>(), "capabilityKey is required.");
        }

        var kind = ParseProtocolKind(request.Input);
        var passthrough = request.Input
            .Where(kv => !IsReserved(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);

        var dto = new ExternalAgentInvocationRequestDto(kind, agentKey.Trim(), capabilityKey.Trim(), passthrough);
        var result = await _client.InvokeAsync(dto, cancellationToken).ConfigureAwait(false);

        if (result.Status != ExternalAgentInvocationStatus.Succeeded || result.OutputPayload is null)
        {
            return new ToolExecutionResult(false, new Dictionary<string, string>(), result.ErrorMessage ?? "External agent invocation failed.");
        }

        var output = new Dictionary<string, string>(result.OutputPayload, StringComparer.OrdinalIgnoreCase)
        {
            ["toolKey"] = request.ToolKey,
            ["protocolKind"] = kind.ToString(),
            ["invocationStatus"] = result.Status.ToString(),
            ["nonCanon"] = "true",
        };

        return new ToolExecutionResult(true, output);
    }

    private static bool IsReserved(string key) =>
        string.Equals(key, "protocolKind", StringComparison.OrdinalIgnoreCase)
        || string.Equals(key, "agentKey", StringComparison.OrdinalIgnoreCase)
        || string.Equals(key, "capabilityKey", StringComparison.OrdinalIgnoreCase);

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
