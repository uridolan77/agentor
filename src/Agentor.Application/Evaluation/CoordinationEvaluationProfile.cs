namespace Agentor.Application.Evaluation;

/// <summary>
/// Evaluation-only coordination strategy labels (orthogonal to <see cref="Agentor.Domain.Enums.CoordinationTopology"/>).
/// Used to compare deterministic harness runs under different tool/skill/policy bindings.
/// </summary>
public enum CoordinationEvaluationProfile
{
    SequentialPipeline,
    SkillWrappedSequential,
    McpToolBoundPlan,
    ExternalAgentTool,
    ReviewGatedPlan
}
