using Agentor.Domain.Enums;

namespace Agentor.Domain.Governance;

/// <summary>Lightweight snapshot of a plan step's execution outcome, captured at review suspension time for context rebuilding on resume.</summary>
public sealed record PlanStepResumeSnapshot(
    Guid PlanStepId,
    string SourceStepId,
    AgentPlanStepStatus FinalStatus,
    bool ToolSucceeded,
    IReadOnlyDictionary<string, string>? Output);

/// <summary>A plan step that had not yet executed when plan execution suspended for human review.</summary>
public sealed record PendingPlanStep(
    Guid PlanStepId,
    string SourceStepId,
    int OrderIndex,
    string ToolKey,
    RecipeStepKind Kind,
    FailureHandlingPolicy OnFailure,
    IReadOnlyDictionary<string, string>? StaticInputParameters,
    StepOutputBinding? OutputBinding);

/// <summary>
/// Cursor recorded on a run when a sequential plan suspends mid-execution pending human review.
/// Enables the approval handler to resume execution at the correct step and continue remaining steps in order.
/// </summary>
public sealed record PlanResumeCursor(
    Guid PlanId,
    Guid BlockedAtPlanStepId,
    string BlockedAtSourceStepId,
    string BlockedAtToolKey,
    IReadOnlyList<PendingPlanStep> RemainingSteps,
    IReadOnlyList<PlanStepResumeSnapshot> CompletedStepHistory,
    DateTimeOffset SuspendedAt)
{
    /// <summary>True when there are unexecuted steps after the blocked step that require resumed execution.</summary>
    public bool HasRemainingSteps => RemainingSteps.Count > 0;
}

/// <summary>Describes the review resume disposition for a run awaiting human review.</summary>
public sealed record ReviewResumeState(
    bool IsMultiStepPlan,
    int RemainingStepCount,
    string BlockedAtSourceStepId,
    string BlockedAtToolKey)
{
    public static ReviewResumeState FromCursor(PlanResumeCursor cursor) =>
        new(cursor.HasRemainingSteps, cursor.RemainingSteps.Count, cursor.BlockedAtSourceStepId, cursor.BlockedAtToolKey);

    public static ReviewResumeState SingleStep(string blockedAtToolKey) =>
        new(false, 0, string.Empty, blockedAtToolKey);
}
