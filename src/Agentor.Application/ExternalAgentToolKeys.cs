namespace Agentor.Application;

public static class ExternalAgentToolKeys
{
    public const string Discover = "external-agent.discover";

    public const string Invoke = "external-agent.invoke";

    public static bool IsDiscover(string toolKey) =>
        string.Equals(toolKey, Discover, StringComparison.OrdinalIgnoreCase);

    public static bool IsInvoke(string toolKey) =>
        string.Equals(toolKey, Invoke, StringComparison.OrdinalIgnoreCase);

    public static bool IsExternalAgentTool(string toolKey) =>
        IsDiscover(toolKey) || IsInvoke(toolKey);
}
