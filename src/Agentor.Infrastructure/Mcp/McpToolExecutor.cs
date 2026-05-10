using Agentor.Application.Abstractions;

namespace Agentor.Infrastructure.Mcp;

/// <summary>
/// Delegates execution to <see cref="IMcpRegistryClient.InvokeToolAsync"/> for a single discovered MCP tool.
/// Policy evaluation remains in the runtime tool pipeline (same as other executors).
/// </summary>
public sealed class McpToolExecutor : IToolExecutor
{
    private readonly IMcpRegistryClient _mcp;
    private readonly string _serverId;
    private readonly string _toolName;

    public McpToolExecutor(IMcpRegistryClient mcp, string serverId, string toolName)
    {
        _mcp = mcp;
        _serverId = serverId;
        _toolName = toolName;
    }

    public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request, CancellationToken cancellationToken)
    {
        var outcome = await _mcp.InvokeToolAsync(_serverId, _toolName, request.Input, cancellationToken).ConfigureAwait(false);
        if (!outcome.Success)
        {
            return new ToolExecutionResult(false, outcome.Output, outcome.ErrorMessage);
        }

        return new ToolExecutionResult(true, outcome.Output);
    }
}
