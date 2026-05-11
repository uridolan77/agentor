using System.Globalization;
using System.Text.Json.Nodes;
using Agentor.Application.Abstractions;
using Agentor.Contracts.ExternalAgents;
using Agentor.Domain;

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
        var flat = request.Input.ToPolicyEvaluationDictionary();
        if (!flat.TryGetValue("agentKey", out var agentKey) || string.IsNullOrWhiteSpace(agentKey))
        {
            return new ToolExecutionResult(false, ToolPayload.Empty, "agentKey is required.");
        }

        if (!flat.TryGetValue("capabilityKey", out var capabilityKey) || string.IsNullOrWhiteSpace(capabilityKey))
        {
            return new ToolExecutionResult(false, ToolPayload.Empty, "capabilityKey is required.");
        }

        var kind = ParseProtocolKind(flat);
        var passthroughBody = new JsonObject();
        foreach (var kv in flat.Where(kv => !IsReserved(kv.Key)))
        {
            passthroughBody[kv.Key] = kv.Value;
        }

        var arguments = new ToolPayload(passthroughBody, request.Input.SchemaId, request.Input.ContentType, null);

        var dto = new ExternalAgentInvocationRequestDto(kind, agentKey.Trim(), capabilityKey.Trim(), arguments);
        var result = await _client.InvokeAsync(dto, cancellationToken).ConfigureAwait(false);

        if (result.Status != ExternalAgentInvocationStatus.Succeeded || result.OutputPayload is null)
        {
            return new ToolExecutionResult(false, ToolPayload.Empty, result.ErrorMessage ?? "External agent invocation failed.");
        }

        var merged = result.OutputPayload.ToPolicyEvaluationDictionary();
        var output = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["toolKey"] = request.ToolKey,
            ["protocolKind"] = kind.ToString(),
            ["invocationStatus"] = result.Status.ToString(),
            ["nonCanon"] = "true",
            ["isNonCanonEvidence"] = result.IsNonCanonEvidence ? "true" : "false",
        };

        foreach (var kv in merged)
        {
            output[kv.Key] = kv.Value;
        }

        return new ToolExecutionResult(
            true,
            new ToolPayload(result.OutputPayload.Body, result.OutputPayload.SchemaId, result.OutputPayload.ContentType, output));
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
