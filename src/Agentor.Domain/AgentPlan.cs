using Agentor.Domain.Enums;

namespace Agentor.Domain;

/// <summary>
/// Instantiated execution structure derived from a recipe. No tool or policy work during construction.
/// </summary>
public sealed class AgentPlan
{
    private readonly List<AgentPlanStep> _steps = new();

    private AgentPlan(
        Guid id,
        Guid recipeId,
        AgentRecipeVersion recipeVersion,
        CoordinationTopology topology,
        FailureHandlingPolicy planFailureHandling,
        AgentPlanStatus status,
        DateTimeOffset createdAt)
    {
        Id = id;
        RecipeId = recipeId;
        RecipeVersion = recipeVersion;
        Topology = topology;
        PlanFailureHandling = planFailureHandling;
        Status = status;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }

    public Guid RecipeId { get; }

    public AgentRecipeVersion RecipeVersion { get; }

    public CoordinationTopology Topology { get; }

    public FailureHandlingPolicy PlanFailureHandling { get; }

    public AgentPlanStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; }

    public IReadOnlyList<AgentPlanStep> Steps => _steps;

    public void RefreshDerivedStatus()
    {
        Status = AgentStateMachine.DerivePlanStatus(_steps);
    }

    public static AgentPlan Instantiate(AgentRecipe recipe, Guid planId, DateTimeOffset createdAt, CoordinationTopology? topologyOverride = null)
    {
        if (recipe is null)
        {
            throw new ArgumentNullException(nameof(recipe));
        }

        var validation = recipe.Validate();
        if (!validation.IsValid)
        {
            throw new InvalidOperationException("Cannot instantiate a plan from an invalid recipe.");
        }

        var topology = topologyOverride ?? recipe.Topology;
        var plan = new AgentPlan(
            planId,
            recipe.Id,
            recipe.Version,
            topology,
            recipe.PlanFailureHandling,
            AgentPlanStatus.Ready,
            createdAt);

        foreach (var def in recipe.Steps.OrderBy(s => s.OrderIndex))
        {
            plan._steps.Add(
                new AgentPlanStep(
                    Guid.NewGuid(),
                    def.StepId.Trim(),
                    def.OrderIndex,
                    def.Kind,
                    def.ToolKey.Trim(),
                    def.Guard,
                    def.InputBinding,
                    def.OutputBinding,
                    def.OnFailure,
                    def.Compensation));
        }

        return plan;
    }
}
