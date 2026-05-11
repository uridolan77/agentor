using Agentor.Infrastructure.Options;

namespace Agentor.Infrastructure.Smoke;

/// <summary>
/// Maps <see cref="IntegrationSmokeOptions"/> family modes onto <c>Agentor:Integrations:*:Mode</c> for the smoke host process.
/// </summary>
public static class IntegrationSmokeConfigurationMerger
{
    /// <summary>
    /// Configuration keys (colon form) applied after base configuration so the smoke tool drives adapter modes explicitly.
    /// </summary>
    public static IReadOnlyDictionary<string, string?> BuildIntegrationModePatches(IntegrationSmokeOptions smoke)
    {
        ArgumentNullException.ThrowIfNull(smoke);
        return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Agentor:Integrations:Athanor:Mode"] = ToAdapterModeString(smoke.Athanor.Mode),
            ["Agentor:Integrations:Conexus:Mode"] = ToAdapterModeString(smoke.Conexus.Mode),
            ["Agentor:Integrations:Mcp:Mode"] = ToAdapterModeString(smoke.Mcp.Mode),
            ["Agentor:Integrations:ExternalAgents:Mode"] = ToAdapterModeString(smoke.ExternalAgents.Mode),
        };
    }

    private static string ToAdapterModeString(SmokeMode mode) =>
        Map(mode).ToString();

    public static IntegrationAdapterMode Map(SmokeMode mode) =>
        mode switch
        {
            SmokeMode.Disabled => IntegrationAdapterMode.Disabled,
            SmokeMode.Fake => IntegrationAdapterMode.Fake,
            SmokeMode.Http => IntegrationAdapterMode.Http,
            _ => IntegrationAdapterMode.Disabled,
        };
}
