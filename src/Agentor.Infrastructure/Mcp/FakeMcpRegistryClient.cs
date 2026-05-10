using Agentor.Application.Abstractions;
using Agentor.Application.Mcp;
using Agentor.Domain.Enums;

namespace Agentor.Infrastructure.Mcp;

/// <summary>
/// In-memory MCP catalog + deterministic tool invocation for harness and local development (no network).
/// </summary>
public sealed class FakeMcpRegistryClient : IMcpRegistryClient
{
    private readonly IReadOnlyDictionary<string, McpServerDescriptor> _servers;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<McpToolDescriptor>> _toolsByServerId;

    public FakeMcpRegistryClient()
    {
        var demo = new McpServerDescriptor("demo-server", "Demo MCP server (fake)");
        _servers = new Dictionary<string, McpServerDescriptor>(StringComparer.OrdinalIgnoreCase)
        {
            [demo.Id] = demo
        };

        _toolsByServerId = new Dictionary<string, IReadOnlyList<McpToolDescriptor>>(StringComparer.OrdinalIgnoreCase)
        {
            [demo.Id] =
            [
                new McpToolDescriptor(demo.Id, "echo", "Echo input text (fake MCP tool).", ToolRiskLevel.Low),
                new McpToolDescriptor(demo.Id, "stats", "Report fake catalog statistics.", ToolRiskLevel.Low)
            ]
        };
    }

    public Task<IReadOnlyList<McpServerDescriptor>> ListServersAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<McpServerDescriptor>>(_servers.Values.OrderBy(s => s.Id, StringComparer.Ordinal).ToList());
    }

    public Task<IReadOnlyList<McpToolDescriptor>> ListToolsAsync(string serverId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_servers.ContainsKey(serverId))
        {
            return Task.FromResult<IReadOnlyList<McpToolDescriptor>>(Array.Empty<McpToolDescriptor>());
        }

        return Task.FromResult(_toolsByServerId.TryGetValue(serverId, out var list)
            ? list
            : Array.Empty<McpToolDescriptor>());
    }

    public Task<McpToolInvocationResult> InvokeToolAsync(
        string serverId,
        string toolName,
        IReadOnlyDictionary<string, string> input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_servers.ContainsKey(serverId))
        {
            return Task.FromResult(new McpToolInvocationResult(
                false,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                $"Unknown MCP server '{serverId}'."));
        }

        var tools = _toolsByServerId.TryGetValue(serverId, out var t) ? t : Array.Empty<McpToolDescriptor>();
        if (!tools.Any(x => string.Equals(x.Name, toolName, StringComparison.OrdinalIgnoreCase)))
        {
            return Task.FromResult(new McpToolInvocationResult(
                false,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                $"Tool '{toolName}' is not registered for server '{serverId}'."));
        }

        if (string.Equals(toolName, "echo", StringComparison.OrdinalIgnoreCase))
        {
            var text = input.TryGetValue("text", out var v) ? v : string.Empty;
            var output = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["result"] = $"mcp:{serverId}:echo:{text}",
                ["tool"] = toolName
            };
            return Task.FromResult(new McpToolInvocationResult(true, output));
        }

        if (string.Equals(toolName, "stats", StringComparison.OrdinalIgnoreCase))
        {
            var output = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["serverCount"] = _servers.Count.ToString(),
                ["toolCount"] = tools.Count.ToString()
            };
            return Task.FromResult(new McpToolInvocationResult(true, output));
        }

        return Task.FromResult(new McpToolInvocationResult(
            false,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            $"Tool '{toolName}' is not implemented in the fake MCP registry."));
    }
}
