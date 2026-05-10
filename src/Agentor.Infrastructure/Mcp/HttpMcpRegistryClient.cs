using System.Net.Http.Json;
using Agentor.Application.Abstractions;
using Agentor.Application.Mcp;
using Agentor.Domain.Enums;
using Agentor.Infrastructure.Http;
using Agentor.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Agentor.Infrastructure.Mcp;

/// <summary>
/// HTTP JSON adapter for <see cref="IMcpRegistryClient"/> (no MCP protocol types in Domain).
/// </summary>
public sealed class HttpMcpRegistryClient(
    IHttpClientFactory httpFactory,
    IOptionsMonitor<AgentorIntegrationsOptions> options)
    : IMcpRegistryClient
{
    internal const string HttpClientName = "integration-mcp";

    public async Task<IReadOnlyList<McpServerDescriptor>> ListServersAsync(CancellationToken cancellationToken = default)
    {
        EnsureHttpMode();
        using var response = await Client().GetAsync("v1/servers", cancellationToken);
        await IntegrationHttpError.ThrowIfUnsuccessfulAsync(response, "MCP", cancellationToken);
        var rows = await response.Content.ReadFromJsonAsync<List<McpServerWire>>(AgentorHttpJson.Options, cancellationToken);
        if (rows is null)
        {
            return [];
        }

        return rows.Select(r => new McpServerDescriptor(r.Id, r.DisplayName)).ToList();
    }

    public async Task<IReadOnlyList<McpToolDescriptor>> ListToolsAsync(string serverId, CancellationToken cancellationToken = default)
    {
        EnsureHttpMode();
        var enc = Uri.EscapeDataString(serverId);
        using var response = await Client().GetAsync($"v1/servers/{enc}/tools", cancellationToken);
        await IntegrationHttpError.ThrowIfUnsuccessfulAsync(response, "MCP", cancellationToken);
        var rows = await response.Content.ReadFromJsonAsync<List<McpToolWire>>(AgentorHttpJson.Options, cancellationToken);
        if (rows is null)
        {
            return [];
        }

        var list = new List<McpToolDescriptor>();
        foreach (var r in rows)
        {
            if (!Enum.TryParse<ToolRiskLevel>(r.NominalRisk, ignoreCase: true, out var risk))
            {
                risk = ToolRiskLevel.Medium;
            }

            list.Add(new McpToolDescriptor(r.ServerId, r.Name, r.Description, risk));
        }

        return list;
    }

    public async Task<McpToolInvocationResult> InvokeToolAsync(
        string serverId,
        string toolName,
        IReadOnlyDictionary<string, string> input,
        CancellationToken cancellationToken = default)
    {
        EnsureHttpMode();
        var encServer = Uri.EscapeDataString(serverId);
        var encTool = Uri.EscapeDataString(toolName);
        using var content = JsonContent.Create(new McpInvokeWire(input), options: AgentorHttpJson.Options);
        using var response = await Client().PostAsync(
            $"v1/servers/{encServer}/tools/{encTool}/invoke",
            content,
            cancellationToken);

        await IntegrationHttpError.ThrowIfUnsuccessfulAsync(response, "MCP", cancellationToken);
        var body = await response.Content.ReadFromJsonAsync<McpInvokeResponseWire>(AgentorHttpJson.Options, cancellationToken);
        if (body is null)
        {
            return new McpToolInvocationResult(false, new Dictionary<string, string>(), "Empty MCP invoke response.");
        }

        return new McpToolInvocationResult(body.Success, body.Output ?? new Dictionary<string, string>(), body.ErrorMessage);
    }

    private HttpClient Client()
    {
        return httpFactory.CreateClient(HttpClientName);
    }

    private void EnsureHttpMode()
    {
        if (options.CurrentValue.Mcp.Mode != IntegrationAdapterMode.Http)
        {
            throw new InvalidOperationException("HttpMcpRegistryClient requires Agentor:Integrations:Mcp:Mode=Http.");
        }
    }

    private sealed record McpServerWire(string Id, string DisplayName);

    private sealed record McpToolWire(
        string ServerId,
        string Name,
        string Description,
        string NominalRisk);

    private sealed record McpInvokeWire(IReadOnlyDictionary<string, string> Input);

    private sealed record McpInvokeResponseWire(bool Success, Dictionary<string, string>? Output, string? ErrorMessage);
}
