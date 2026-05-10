using Agentor.Domain.Enums;

namespace Agentor.Infrastructure;

public sealed class RuntimePolicyOptions
{
    public const string SectionName = "Agentor:RuntimePolicy";

    public List<string> AllowedToolKeys { get; set; } = [];

    public List<string> DeniedToolKeys { get; set; } = [];

    public string MaxAutoApproveRisk { get; set; } = nameof(ToolRiskLevel.High);
}