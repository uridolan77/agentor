using Agentor.Domain.Enums;

namespace Agentor.Infrastructure;

public sealed class RuntimePolicyOptions
{
    public const string SectionName = "Agentor:RuntimePolicy";

    public List<string> AllowedToolKeys { get; set; } = [];

    public List<string> DeniedToolKeys { get; set; } = [];

    public string MaxAutoApproveRisk { get; set; } = nameof(ToolRiskLevel.High);

    /// <summary>
    /// When set, model-call tool input may include declaredCostUnits; values above this cap are denied.
    /// </summary>
    public decimal? MaxDeclaredModelCallCostUnits { get; set; }

    /// <summary>
    /// When set, model-call tool input may include declaredLatencyMs; values above this cap are denied.
    /// </summary>
    public int? MaxDeclaredModelCallLatencyMs { get; set; }
}