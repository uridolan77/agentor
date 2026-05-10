using Agentor.Domain.Enums;

namespace Agentor.Domain;

/// <summary>
/// Reusable declarative recipe template. Construction performs no tool or policy work.
/// </summary>
public sealed class AgentRecipe
{
    private AgentRecipe(
        Guid id,
        string name,
        AgentRecipeVersion version,
        CoordinationTopology topology,
        FailureHandlingPolicy planFailureHandling,
        CoordinationProfileRef? profileRef,
        IReadOnlyList<RecipeStepDefinition> steps)
    {
        Id = id;
        Name = name;
        Version = version;
        Topology = topology;
        PlanFailureHandling = planFailureHandling;
        ProfileRef = profileRef;
        Steps = steps;
    }

    public Guid Id { get; }

    public string Name { get; }

    public AgentRecipeVersion Version { get; }

    public CoordinationTopology Topology { get; }

    public FailureHandlingPolicy PlanFailureHandling { get; }

    public CoordinationProfileRef? ProfileRef { get; }

    public IReadOnlyList<RecipeStepDefinition> Steps { get; }

    public PlanValidationResult Validate()
    {
        var step = RecipePlanValidation.ValidateSteps(Steps);
        if (!step.IsValid)
        {
            return step;
        }

        return RecipePlanValidation.ValidateGuards(Steps);
    }

    public static bool TryCreate(
        Guid id,
        string name,
        AgentRecipeVersion version,
        CoordinationTopology topology,
        IReadOnlyList<RecipeStepDefinition> steps,
        CoordinationProfileRef? profileRef,
        out AgentRecipe? recipe,
        out PlanValidationResult validation,
        FailureHandlingPolicy planFailureHandling = FailureHandlingPolicy.FailFast)
    {
        var issues = new List<PlanValidationIssue>();

        if (string.IsNullOrWhiteSpace(name))
        {
            issues.Add(new PlanValidationIssue("RECIPE_NAME_REQUIRED", "Recipe name is required."));
        }

        var stepValidation = RecipePlanValidation.ValidateSteps(steps);
        issues.AddRange(stepValidation.Issues);
        if (issues.Count > 0)
        {
            validation = PlanValidationResult.FromIssues(issues);
            recipe = null;
            return false;
        }

        var guardValidation = RecipePlanValidation.ValidateGuards(steps);
        issues.AddRange(guardValidation.Issues);
        validation = PlanValidationResult.FromIssues(issues);
        if (!validation.IsValid)
        {
            recipe = null;
            return false;
        }

        recipe = new AgentRecipe(
            id,
            name.Trim(),
            version,
            topology,
            planFailureHandling,
            profileRef,
            NormalizeOrder(steps));
        return true;
    }

    private static IReadOnlyList<RecipeStepDefinition> NormalizeOrder(IReadOnlyList<RecipeStepDefinition> steps)
    {
        return steps.OrderBy(s => s.OrderIndex).ToList();
    }
}

internal static class RecipePlanValidation
{
    internal static PlanValidationResult ValidateSteps(IReadOnlyList<RecipeStepDefinition> steps)
    {
        var issues = new List<PlanValidationIssue>();

        if (steps is null || steps.Count == 0)
        {
            issues.Add(new PlanValidationIssue("RECIPE_NO_STEPS", "A recipe must declare at least one step."));
            return PlanValidationResult.FromIssues(issues);
        }

        var seenIndexes = new HashSet<int>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var step in steps.OrderBy(s => s.OrderIndex))
        {
            if (string.IsNullOrWhiteSpace(step.StepId))
            {
                issues.Add(new PlanValidationIssue("STEP_ID_REQUIRED", "Each step requires a non-empty step id."));
            }
            else if (!seenIds.Add(step.StepId.Trim()))
            {
                issues.Add(new PlanValidationIssue("DUPLICATE_STEP_ID", $"Duplicate step id '{step.StepId}'.", step.StepId));
            }

            if (!seenIndexes.Add(step.OrderIndex))
            {
                issues.Add(new PlanValidationIssue("DUPLICATE_STEP_INDEX", $"Duplicate order index {step.OrderIndex}.", step.StepId));
            }

            if (step.Kind == RecipeStepKind.Tool)
            {
                if (string.IsNullOrWhiteSpace(step.ToolKey))
                {
                    issues.Add(new PlanValidationIssue("TOOL_KEY_INVALID", "Tool steps require a non-empty tool key.", step.StepId));
                }

                if (!string.IsNullOrWhiteSpace(step.InvokedSkillKey) || step.InvokedSkillVersion is not null)
                {
                    issues.Add(new PlanValidationIssue(
                        "TOOL_STEP_SKILL_METADATA",
                        "Tool steps must not declare invoked skill metadata.",
                        step.StepId));
                }
            }
            else if (step.Kind == RecipeStepKind.Skill)
            {
                if (string.IsNullOrWhiteSpace(step.InvokedSkillKey))
                {
                    issues.Add(new PlanValidationIssue("SKILL_KEY_INVALID", "Skill steps require a non-empty invoked skill key.", step.StepId));
                }

                if (step.InvokedSkillVersion is null)
                {
                    issues.Add(new PlanValidationIssue("SKILL_VERSION_INVALID", "Skill steps require an invoked skill version.", step.StepId));
                }

                if (!string.IsNullOrWhiteSpace(step.ToolKey))
                {
                    issues.Add(new PlanValidationIssue(
                        "SKILL_STEP_TOOLKEY",
                        "Skill steps must not declare a tool key (use InvokedSkillKey / InvokedSkillVersion).",
                        step.StepId));
                }
            }
        }

        return PlanValidationResult.FromIssues(issues);
    }

    internal static PlanValidationResult ValidateGuards(IReadOnlyList<RecipeStepDefinition> steps)
    {
        if (steps is null || steps.Count == 0)
        {
            return PlanValidationResult.Success;
        }

        var issues = new List<PlanValidationIssue>();
        var ordered = steps.OrderBy(s => s.OrderIndex).ToList();
        var idToOrder = ordered.ToDictionary(s => s.StepId.Trim(), s => s.OrderIndex, StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < ordered.Count; i++)
        {
            var step = ordered[i];
            var kind = step.Guard?.Kind ?? StepGuardKind.Always;

            if (i == 0 && kind is not StepGuardKind.Always)
            {
                if (kind is StepGuardKind.PreviousStepSucceeded
                    or StepGuardKind.PreviousStepFailed
                    or StepGuardKind.PreviousStepOutputExists
                    or StepGuardKind.PreviousStepOutputEquals
                    or StepGuardKind.AllPreviousStepsSucceeded)
                {
                    issues.Add(new PlanValidationIssue(
                        "GUARD_NO_PREVIOUS_STEP",
                        $"Guard '{kind}' is not valid on the first step.",
                        step.StepId));
                }
            }

            if (kind == StepGuardKind.PreviousStepOutputExists)
            {
                if (string.IsNullOrWhiteSpace(step.Guard?.OutputKey))
                {
                    issues.Add(new PlanValidationIssue(
                        "GUARD_OUTPUT_KEY_REQUIRED",
                        "PreviousStepOutputExists requires OutputKey.",
                        step.StepId));
                }
            }

            if (kind == StepGuardKind.PreviousStepOutputEquals)
            {
                if (string.IsNullOrWhiteSpace(step.Guard?.OutputKey)
                    || string.IsNullOrWhiteSpace(step.Guard?.ExpectedOutputValue))
                {
                    issues.Add(new PlanValidationIssue(
                        "GUARD_EQUALS_PARAMS",
                        "PreviousStepOutputEquals requires OutputKey and ExpectedOutputValue.",
                        step.StepId));
                }
            }

            var refId = step.Guard?.ReferenceStepId?.Trim();
            if (!string.IsNullOrEmpty(refId))
            {
                if (!idToOrder.TryGetValue(refId, out var refOrder))
                {
                    issues.Add(new PlanValidationIssue(
                        "GUARD_UNKNOWN_REFERENCE_STEP",
                        $"Guard references unknown step id '{refId}'.",
                        step.StepId));
                }
                else if (refOrder >= step.OrderIndex)
                {
                    issues.Add(new PlanValidationIssue(
                        "GUARD_REFERENCE_NOT_PRIOR",
                        "Guard reference step must be strictly before the current step in plan order.",
                        step.StepId));
                }
            }
        }

        return PlanValidationResult.FromIssues(issues);
    }
}
