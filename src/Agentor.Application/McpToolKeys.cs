namespace Agentor.Application;

/// <summary>
/// Stable Agentor tool keys for MCP-backed tools registered from <see cref="Abstractions.IMcpRegistryClient"/> discovery.
/// </summary>
public static class McpToolKeys
{
    public const string Prefix = "mcp.";

    public static bool IsMcpToolKey(string toolKey) =>
        toolKey.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);

    public static string Format(string serverId, string toolName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        return Prefix + serverId.Trim() + "." + toolName.Trim();
    }
}
