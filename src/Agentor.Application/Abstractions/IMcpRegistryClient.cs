using Agentor.Application.Mcp;

namespace Agentor.Application.Abstractions;

/// <summary>
/// Port for MCP server and tool catalog discovery. Real transport (stdio/SSE) stays in a future adapter; default stack uses an in-memory fake registry.
/// </summary>
public interface IMcpRegistryClient
{
    Task<IReadOnlyList<McpServerDescriptor>> ListServersAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<McpToolDescriptor>> ListToolsAsync(string serverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a tool discovered via this registry (fake implementation is deterministic; real adapter would perform MCP JSON-RPC).
    /// </summary>
    Task<McpToolInvocationResult> InvokeToolAsync(
        string serverId,
        string toolName,
        IReadOnlyDictionary<string, string> input,
        CancellationToken cancellationToken = default);
}
