using Agentor.Domain.Enums;

namespace Agentor.Infrastructure;

public sealed class RuntimePolicyOptions
{
    public const string SectionName = "Agentor:RuntimePolicy";

    public List<string> AllowedToolKeys { get; set; } = [];

    public List<string> DeniedToolKeys { get; set; } = [];

    public string MaxAutoApproveRisk { get; set; } = nameof(ToolRiskLevel.High);

    /// <summary>
    /// When set, optional tool input key <c>declaredCostUnits</c> is compared before execution (declared intent).
    /// Missing keys do not trigger denial — this is not enforcement of actual post-execution spend.
    /// </summary>
    public decimal? MaxDeclaredModelCallCostUnits { get; set; }

    /// <summary>
    /// When set, optional tool input key <c>declaredLatencyMs</c> is compared before execution (declared intent).
    /// Missing keys do not trigger denial — actual latency is established only after the gateway returns.
    /// </summary>
    public int? MaxDeclaredModelCallLatencyMs { get; set; }

    /// <summary>
    /// Optional composable profile (PR52). When null, the flat properties above are the effective policy.
    /// When set, profile fields replace the flat lists and thresholds for evaluation (MCP / external-agent rules apply from the profile).
    /// </summary>
    public PolicyProfileRules? ActiveProfile { get; set; }
}

/// <summary>Composable runtime policy profile (PR52).</summary>
public sealed class PolicyProfileRules
{
    public List<string> AllowedToolKeys { get; set; } = [];

    public List<string> DeniedToolKeys { get; set; } = [];

    public string MaxAutoApproveRisk { get; set; } = nameof(ToolRiskLevel.High);

    public decimal? MaxDeclaredModelCallCostUnits { get; set; }

    public int? MaxDeclaredModelCallLatencyMs { get; set; }

    /// <summary>Exact MCP tool keys (for example <c>mcp.server.tool</c>) denied regardless of registry risk.</summary>
    public List<string> McpDeniedToolKeys { get; set; } = [];

    /// <summary>External-agent tool keys denied (for example <c>external-agent.invoke</c>).</summary>
    public List<string> ExternalAgentDeniedToolKeys { get; set; } = [];
}
