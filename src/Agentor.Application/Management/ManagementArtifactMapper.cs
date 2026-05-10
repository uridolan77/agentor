using Agentor.Contracts;
using Agentor.Domain;
using Agentor.Domain.Enums;

namespace Agentor.Application.Management;

public static class ManagementArtifactMapper
{
    public static RecipeArtifactResponseDto ToResponse(this AgentRecipe recipe)
    {
        var profile = recipe.ProfileRef is null
            ? null
            : new CoordinationProfileRefRequestDto(recipe.ProfileRef.ProfileKey, recipe.ProfileRef.Version);

        var steps = recipe.Steps
            .OrderBy(s => s.OrderIndex)
            .Select(s => s.ToResponse())
            .ToList();

        return new RecipeArtifactResponseDto(
            recipe.Id,
            recipe.Name,
            recipe.Version.Value,
            recipe.Topology,
            recipe.PlanFailureHandling,
            profile,
            steps);
    }

    public static RecipeStepResponseDto ToResponse(this RecipeStepDefinition s)
    {
        var guard = s.Guard is null
            ? null
            : new StepGuardRequestDto(
                s.Guard.Kind,
                s.Guard.ReferenceStepId,
                s.Guard.ExpectedOutputValue,
                s.Guard.OutputKey);

        IReadOnlyDictionary<string, string>? inputParams = s.InputBinding is null || s.InputBinding.Parameters.Count == 0
            ? null
            : new Dictionary<string, string>(s.InputBinding.Parameters, StringComparer.OrdinalIgnoreCase);

        var outKey = s.OutputBinding?.NormalizedKey;

        return new RecipeStepResponseDto(
            s.StepId,
            s.OrderIndex,
            s.Kind,
            s.ToolKey,
            guard,
            inputParams,
            outKey,
            s.OnFailure,
            s.Compensation?.HookId,
            s.InvokedSkillKey,
            s.InvokedSkillVersion?.Value);
    }

    public static PlanArtifactResponseDto ToResponse(this AgentPlan plan)
    {
        var steps = plan.Steps
            .OrderBy(s => s.OrderIndex)
            .Select(s => new PlanStepArtifactResponseDto(
                s.Id,
                s.SourceStepId,
                s.OrderIndex,
                s.Kind,
                s.ToolKey,
                s.InvokedSkillKey,
                s.InvokedSkillVersion?.Value,
                s.Status))
            .ToList();

        return new PlanArtifactResponseDto(
            plan.Id,
            plan.RecipeId,
            plan.RecipeVersion.Value,
            plan.Topology,
            plan.PlanFailureHandling,
            plan.Status,
            plan.CreatedAt,
            steps);
    }

    public static SkillPackageDetailResponseDto ToDetailResponse(this SkillPackage package)
    {
        var steps = package.ProcedureSteps
            .OrderBy(s => s.OrderIndex)
            .Select(s => new SkillProcedureStepRequestDto(s.StepId, s.OrderIndex, s.Name, s.Kind, s.ToolKey))
            .ToList();

        return new SkillPackageDetailResponseDto(
            package.Id,
            package.SkillKey,
            package.Version.Value,
            package.Name,
            package.Purpose,
            steps,
            package.DeclaredToolKeys);
    }

    public static bool TryMap(CreateRecipeRequestDto dto, Guid recipeId, out AgentRecipe? recipe, out PlanValidationResult validation)
    {
        var steps = new List<RecipeStepDefinition>();
        foreach (var s in dto.Steps.OrderBy(x => x.OrderIndex))
        {
            StepGuardDefinition? guard = null;
            if (s.Guard is not null)
            {
                guard = new StepGuardDefinition(
                    s.Guard.Kind,
                    s.Guard.ReferenceStepId,
                    s.Guard.ExpectedOutputValue,
                    s.Guard.OutputKey);
            }

            StepInputBinding? inputBinding = null;
            if (s.InputParameters is not null && s.InputParameters.Count > 0)
            {
                inputBinding = new StepInputBinding(new Dictionary<string, string>(s.InputParameters, StringComparer.OrdinalIgnoreCase));
            }

            StepOutputBinding? outputBinding = string.IsNullOrWhiteSpace(s.OutputKey)
                ? null
                : new StepOutputBinding(s.OutputKey.Trim());

            CompensationHookDefinition? comp = string.IsNullOrWhiteSpace(s.CompensationHookId)
                ? null
                : new CompensationHookDefinition(s.CompensationHookId.Trim(), s.CompensationDescription);

            AgentRecipeVersion? skillVersion = string.IsNullOrWhiteSpace(s.InvokedSkillVersion)
                ? null
                : AgentRecipeVersion.Parse(s.InvokedSkillVersion);

            steps.Add(new RecipeStepDefinition(
                s.StepId.Trim(),
                s.OrderIndex,
                s.Kind,
                s.ToolKey?.Trim() ?? string.Empty,
                guard,
                inputBinding,
                outputBinding,
                s.OnFailure,
                comp,
                string.IsNullOrWhiteSpace(s.InvokedSkillKey) ? null : s.InvokedSkillKey.Trim(),
                skillVersion));
        }

        CoordinationProfileRef? profileRef = null;
        if (dto.ProfileRef is not null && !string.IsNullOrWhiteSpace(dto.ProfileRef.ProfileKey))
        {
            profileRef = new CoordinationProfileRef(dto.ProfileRef.ProfileKey.Trim(), dto.ProfileRef.Version?.Trim());
        }

        return AgentRecipe.TryCreate(
            recipeId,
            dto.Name,
            AgentRecipeVersion.Parse(dto.Version),
            dto.Topology,
            steps,
            profileRef,
            out recipe,
            out validation,
            dto.PlanFailureHandling);
    }

    public static bool TryMap(CreateSkillPackageRequestDto dto, Guid packageId, out SkillPackage? package, out PlanValidationResult validation)
    {
        var steps = dto.ProcedureSteps
            .OrderBy(s => s.OrderIndex)
            .Select(s => new SkillProcedureStepDefinition(s.StepId.Trim(), s.OrderIndex, s.Name.Trim(), s.Kind, s.ToolKey?.Trim()))
            .ToList();

        return SkillPackage.TryCreate(
            packageId,
            dto.SkillKey,
            AgentRecipeVersion.Parse(dto.Version),
            dto.Name,
            dto.Purpose,
            steps,
            out package,
            out validation);
    }
}
