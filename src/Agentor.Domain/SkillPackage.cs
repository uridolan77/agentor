using Agentor.Domain.Enums;

namespace Agentor.Domain;

public sealed class SkillPackage
{
    private SkillPackage(
        Guid id,
        string skillKey,
        AgentRecipeVersion version,
        string name,
        string purpose,
        IReadOnlyList<SkillProcedureStepDefinition> procedureSteps)
    {
        Id = id;
        SkillKey = skillKey;
        Version = version;
        Name = name;
        Purpose = purpose;
        ProcedureSteps = procedureSteps;
    }

    public Guid Id { get; }

    public string SkillKey { get; }

    public AgentRecipeVersion Version { get; }

    public string Name { get; }

    public string Purpose { get; }

    public IReadOnlyList<SkillProcedureStepDefinition> ProcedureSteps { get; }

    public IReadOnlyList<string> DeclaredToolKeys =>
        ProcedureSteps
            .OrderBy(s => s.OrderIndex)
            .Where(s => s.Kind == SkillProcedureStepKind.ToolRef && !string.IsNullOrWhiteSpace(s.ToolKey))
            .Select(s => s.ToolKey!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public PlanValidationResult Validate() => SkillPackageValidation.ValidateProcedure(ProcedureSteps);

    public static bool TryCreate(
        Guid id,
        string skillKey,
        AgentRecipeVersion version,
        string name,
        string purpose,
        IReadOnlyList<SkillProcedureStepDefinition> procedureSteps,
        out SkillPackage? package,
        out PlanValidationResult validation)
    {
        var issues = new List<PlanValidationIssue>();

        if (string.IsNullOrWhiteSpace(skillKey))
        {
            issues.Add(new PlanValidationIssue("SKILL_KEY_REQUIRED", "Skill key is required."));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            issues.Add(new PlanValidationIssue("SKILL_NAME_REQUIRED", "Skill name is required."));
        }

        if (string.IsNullOrWhiteSpace(purpose))
        {
            issues.Add(new PlanValidationIssue("SKILL_PURPOSE_REQUIRED", "Skill purpose is required."));
        }

        var procValidation = SkillPackageValidation.ValidateProcedure(procedureSteps);
        issues.AddRange(procValidation.Issues);

        validation = PlanValidationResult.FromIssues(issues);
        if (!validation.IsValid)
        {
            package = null;
            return false;
        }

        package = new SkillPackage(
            id,
            skillKey.Trim(),
            version,
            name.Trim(),
            purpose.Trim(),
            NormalizeOrder(procedureSteps));
        return true;
    }

    private static IReadOnlyList<SkillProcedureStepDefinition> NormalizeOrder(IReadOnlyList<SkillProcedureStepDefinition> steps)
    {
        return steps.OrderBy(s => s.OrderIndex).ToList();
    }
}

internal static class SkillPackageValidation
{
    internal static PlanValidationResult ValidateProcedure(IReadOnlyList<SkillProcedureStepDefinition>? steps)
    {
        var issues = new List<PlanValidationIssue>();

        if (steps is null || steps.Count == 0)
        {
            issues.Add(new PlanValidationIssue("SKILL_NO_PROCEDURE_STEPS", "A skill package must declare at least one procedure step."));
            return PlanValidationResult.FromIssues(issues);
        }

        var seenIndexes = new HashSet<int>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var step in steps.OrderBy(s => s.OrderIndex))
        {
            if (string.IsNullOrWhiteSpace(step.StepId))
            {
                issues.Add(new PlanValidationIssue("SKILL_STEP_ID_REQUIRED", "Each procedure step requires a non-empty step id."));
            }
            else if (!seenIds.Add(step.StepId.Trim()))
            {
                issues.Add(new PlanValidationIssue("SKILL_DUPLICATE_STEP_ID", $"Duplicate procedure step id '{step.StepId}'.", step.StepId));
            }

            if (!seenIndexes.Add(step.OrderIndex))
            {
                issues.Add(new PlanValidationIssue("SKILL_DUPLICATE_STEP_INDEX", $"Duplicate order index {step.OrderIndex}.", step.StepId));
            }

            if (string.IsNullOrWhiteSpace(step.Name))
            {
                issues.Add(new PlanValidationIssue("SKILL_STEP_NAME_REQUIRED", "Each procedure step requires a non-empty name.", step.StepId));
            }

            switch (step.Kind)
            {
                case SkillProcedureStepKind.Segment:
                    if (!string.IsNullOrWhiteSpace(step.ToolKey))
                    {
                        issues.Add(new PlanValidationIssue(
                            "SKILL_SEGMENT_TOOLKEY",
                            "Segment procedure steps must not declare a tool key.",
                            step.StepId));
                    }

                    break;
                case SkillProcedureStepKind.ToolRef:
                    if (string.IsNullOrWhiteSpace(step.ToolKey))
                    {
                        issues.Add(new PlanValidationIssue(
                            "SKILL_TOOLREF_KEY_REQUIRED",
                            "ToolRef procedure steps require a non-empty tool key.",
                            step.StepId));
                    }

                    break;
                default:
                    issues.Add(new PlanValidationIssue("SKILL_STEP_KIND_UNKNOWN", "Unknown procedure step kind.", step.StepId));
                    break;
            }
        }

        return PlanValidationResult.FromIssues(issues);
    }
}
