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
    StepOutputBinding? OutputBinding,
    string? InvokedSkillKey = null,
    AgentRecipeVersion? InvokedSkillVersion = null);

/// <summary>
/// Identifies an inner tool invocation inside a skill procedure when execution suspends for review mid-procedure.
/// </summary>
public sealed record SkillInnerToolCheckpoint(
    string ProcedureStepId,
    string ToolKey,
    int ProcedureOrderIndex);

/// <summary>
/// Non-canon scratch state carried across skill procedure suspension points (tool outputs are evidence only).
/// </summary>
public sealed record SkillProcedureResumeState(
    IReadOnlyDictionary<string, string>? LastInnerToolOutput);

/// <summary>
/// Resume coordinates for a partially executed skill plan step. Approval resumes the blocked inner tool only;
/// it does not grant license to skip policy checks on subsequent inner tools.
/// </summary>
public sealed record SkillResumeCursor(
    PendingPlanStep SkillPlanStep,
    SkillInnerToolCheckpoint BlockedAtInnerTool,
    SkillProcedureResumeState State);

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
    DateTimeOffset SuspendedAt,
    SkillResumeCursor? SkillContinuation = null)
{
    /// <summary>True when there are unexecuted plan steps after the blocked step.</summary>
    public bool HasRemainingSteps => RemainingSteps.Count > 0;

    /// <summary>True when either remaining plan steps exist or a skill procedure must continue after an inner-tool approval.</summary>
    public bool HasContinuationWork => RemainingSteps.Count > 0 || SkillContinuation is not null;
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
