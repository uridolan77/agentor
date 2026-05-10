using Agentor.Domain.Enums;

namespace Agentor.Domain;

public sealed class ToolDefinition
{
    public ToolDefinition(string key, string displayName, string description, ToolRiskLevel riskLevel)
    {
        Key = key;
        DisplayName = displayName;
        Description = description;
        RiskLevel = riskLevel;
    }

    public string Key { get; }

    public string DisplayName { get; }

    public string Description { get; }

    public ToolRiskLevel RiskLevel { get; }
}