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
        CompensationHookDefinition? compensationHook)
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
        Status = AgentPlanStepStatus.Pending;
        CompensationStatus = CompensationStatus.None;
    }

    public Guid Id { get; }

    public string SourceStepId { get; }

    public int OrderIndex { get; }

    public RecipeStepKind Kind { get; }

    public string ToolKey { get; }

    public StepGuardDefinition? Guard { get; }

    public StepInputBinding? InputBinding { get; }

    public StepOutputBinding? OutputBinding { get; }

    public FailureHandlingPolicy OnFailure { get; }

    public CompensationHookDefinition? CompensationHook { get; }

    public CompensationStatus CompensationStatus { get; set; }

    public AgentPlanStepStatus Status { get; set; }
}
