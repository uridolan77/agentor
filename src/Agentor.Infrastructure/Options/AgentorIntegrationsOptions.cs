namespace Agentor.Infrastructure.Options;

/// <summary>
/// Root integration configuration: <c>Agentor:Integrations</c>.
/// </summary>
public sealed class AgentorIntegrationsOptions
{
    public const string SectionName = "Agentor:Integrations";

    public IntegrationFamilyOptions Athanor { get; set; } = new();

    public IntegrationFamilyOptions Conexus { get; set; } = new();

    public IntegrationFamilyOptions Mcp { get; set; } = new();

    public IntegrationFamilyOptions ExternalAgents { get; set; } = new();
}
