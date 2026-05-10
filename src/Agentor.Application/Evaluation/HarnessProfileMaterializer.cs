using Agentor.Domain;
using Agentor.Domain.Enums;

namespace Agentor.Application.Evaluation;

/// <summary>
/// Builds <see cref="AgentRun"/> + <see cref="AgentPlan"/> for harness fixtures under evaluation coordination profiles.
/// </summary>
public static class HarnessProfileMaterializer
{
    public const string Phase14EvalSkillKey = "phase14.eval-skill";

    public static readonly AgentRecipeVersion Phase14EvalSkillVersion = AgentRecipeVersion.Parse("1.0.0");

    /// <summary>
    /// Creates the canonical Phase 14 evaluation skill (segment + inner PR1 fake tool).
    /// </summary>
    public static bool TryCreatePhase14EvalSkill(out SkillPackage? package, out PlanValidationResult validation)
    {
        return SkillPackage.TryCreate(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Phase14EvalSkillKey,
            Phase14EvalSkillVersion,
            "Phase 14 eval skill",
            "Deterministic procedure for coordination profile comparisons.",
            [
                new SkillProcedureStepDefinition("p1", 1, "Intro", SkillProcedureStepKind.Segment),
                new SkillProcedureStepDefinition("p2", 2, "Echo", SkillProcedureStepKind.ToolRef, WellKnownToolKeys.Pr1FakeTool)
            ],
            out package,
            out validation);
    }

    public static bool TryCreateRunAndPlan(
        HarnessFixtureDefinition fixture,
        CoordinationEvaluationProfile profile,
        DateTimeOffset now,
        out AgentRun? run,
        out AgentPlan? plan,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        run = AgentRun.Start(
            Guid.NewGuid(),
            fixture.AgentName,
            fixture.Objective,
            fixture.TraceId,
            now);

        return TryCreateRecipe(fixture, profile, Guid.NewGuid(), now, out plan, out error);
    }

    private static bool TryCreateRecipe(
        HarnessFixtureDefinition fixture,
        CoordinationEvaluationProfile profile,
        Guid recipeId,
        DateTimeOffset now,
        out AgentPlan? plan,
        out string? error)
    {
        var step = profile switch
        {
            CoordinationEvaluationProfile.SequentialPipeline => BuildToolStep(fixture, fixture.ToolKey),
            CoordinationEvaluationProfile.SkillWrappedSequential => new RecipeStepDefinition(
                fixture.ToolStepId,
                fixture.ToolStepOrder,
                RecipeStepKind.Skill,
                string.Empty,
                Guard: null,
                InputBinding: null,
                InvokedSkillKey: Phase14EvalSkillKey,
                InvokedSkillVersion: Phase14EvalSkillVersion),
            CoordinationEvaluationProfile.McpToolBoundPlan => new RecipeStepDefinition(
                fixture.ToolStepId,
                fixture.ToolStepOrder,
                RecipeStepKind.Tool,
                McpToolKeys.Format("demo-server", "echo"),
                Guard: null,
                InputBinding: new StepInputBinding(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["text"] = "phase14-mcp-echo"
                })),
            CoordinationEvaluationProfile.ExternalAgentTool => new RecipeStepDefinition(
                fixture.ToolStepId,
                fixture.ToolStepOrder,
                RecipeStepKind.Tool,
                ExternalAgentToolKeys.Invoke,
                Guard: null,
                InputBinding: new StepInputBinding(
                    fixture.ToolStepParameters is { Count: > 0 }
                        ? new Dictionary<string, string>(fixture.ToolStepParameters, StringComparer.OrdinalIgnoreCase)
                        : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["agentKey"] = "alpha-agent",
                            ["capabilityKey"] = "reply"
                        })),
            CoordinationEvaluationProfile.ReviewGatedPlan => BuildToolStep(fixture, WellKnownToolKeys.Pr1HighRiskFakeTool),
            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, null)
        };

        if (!AgentRecipe.TryCreate(
                recipeId,
                fixture.RecipeName + "-" + profile,
                AgentRecipeVersion.Parse(fixture.RecipeVersion),
                fixture.Topology,
                [step],
                null,
                out var recipe,
                out var val))
        {
            plan = null;
            error = string.Join("; ", val.Issues.Select(i => i.Code));
            return false;
        }

        if (!val.IsValid)
        {
            plan = null;
            error = string.Join("; ", val.Issues.Select(i => i.Code));
            return false;
        }

        plan = AgentPlan.Instantiate(recipe!, Guid.NewGuid(), now);
        error = null;
        return true;
    }

    private static RecipeStepDefinition BuildToolStep(HarnessFixtureDefinition fixture, string toolKey)
    {
        var key = string.IsNullOrWhiteSpace(toolKey) ? WellKnownToolKeys.Pr1FakeTool : toolKey;
        StepInputBinding? binding = fixture.ToolStepParameters is { Count: > 0 }
            ? new StepInputBinding(fixture.ToolStepParameters)
            : null;
        return new RecipeStepDefinition(
            fixture.ToolStepId,
            fixture.ToolStepOrder,
            RecipeStepKind.Tool,
            key,
            Guard: null,
            InputBinding: binding);
    }
}
