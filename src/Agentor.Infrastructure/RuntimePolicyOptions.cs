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
}