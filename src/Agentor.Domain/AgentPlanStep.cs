using Agentor.Domain.Enums;

namespace Agentor.Domain;

public sealed class AgentPlanStep
{
    public AgentPlanStep(
        Guid id,
        string sourceStepId,
        int orderIndex,
        RecipeStepKind kind,
        string toolKey,
        StepGuardDefinition? guard,
        StepInputBinding? inputBinding,
        StepOutputBinding? outputBinding,
        FailureHandlingPolicy onFailure,
        CompensationHookDefinition? compensationHook,
        string? invokedSkillKey = null,
        AgentRecipeVersion? invokedSkillVersion = null)
    {
        Id = id;
        SourceStepId = sourceStepId;
        OrderIndex = orderIndex;
        Kind = kind;
        ToolKey = toolKey;
        Guard = guard;
        InputBinding = inputBinding;
        OutputBinding = outputBinding;
        OnFailure = onFailure;
        CompensationHook = compensationHook;
        InvokedSkillKey = invokedSkillKey;
        InvokedSkillVersion = invokedSkillVersion;
        Status = AgentPlanStepStatus.Pending;
        CompensationStatus = CompensationStatus.None;
    }

    public Guid Id { get; }

    public string SourceStepId { get; }

    public int OrderIndex { get; }

    public RecipeStepKind Kind { get; }

    public string ToolKey { get; }

    /// <summary>When <see cref="Kind"/> is <see cref="RecipeStepKind.Skill"/>, the logical skill key to resolve.</summary>
    public string? InvokedSkillKey { get; }

    /// <summary>When <see cref="Kind"/> is <see cref="RecipeStepKind.Skill"/>, the skill package version to resolve.</summary>
    public AgentRecipeVersion? InvokedSkillVersion { get; }

    public StepGuardDefinition? Guard { get; }

    public StepInputBinding? InputBinding { get; }

    public StepOutputBinding? OutputBinding { get; }

    public FailureHandlingPolicy OnFailure { get; }

    public CompensationHookDefinition? CompensationHook { get; }

    public CompensationStatus CompensationStatus { get; set; }

    public AgentPlanStepStatus Status { get; set; }
}
