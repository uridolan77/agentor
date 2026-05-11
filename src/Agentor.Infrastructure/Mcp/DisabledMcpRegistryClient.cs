using Agentor.Application.Abstractions;
using Agentor.Application.Mcp;
using Agentor.Domain;

namespace Agentor.Infrastructure.Mcp;

public sealed class DisabledMcpRegistryClient : IMcpRegistryClient
{
    private static readonly InvalidOperationException Error = new(
        "MCP integration is disabled (configure Agentor:Integrations:Mcp:Mode).");

    public Task<IReadOnlyList<McpServerDescriptor>> ListServersAsync(CancellationToken cancellationToken = default) =>
        Task.FromException<IReadOnlyList<McpServerDescriptor>>(Error);

    public Task<IReadOnlyList<McpToolDescriptor>> ListToolsAsync(string serverId, CancellationToken cancellationToken = default) =>
        Task.FromException<IReadOnlyList<McpToolDescriptor>>(Error);

    public Task<McpToolInvocationResult> InvokeToolAsync(
        string serverId,
        string toolName,
        ToolPayload arguments,
        CancellationToken cancellationToken = default) =>
        Task.FromException<McpToolInvocationResult>(Error);
}
