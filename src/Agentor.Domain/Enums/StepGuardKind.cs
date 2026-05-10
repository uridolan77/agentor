namespace Agentor.Domain.Enums;

/// <summary>
/// Deterministic guard kinds; no scripting or external evaluation.
/// </summary>
public enum StepGuardKind
{
    Always,

    PreviousStepSucceeded,
    PreviousStepFailed,
    PreviousStepOutputExists,
    PreviousStepOutputEquals,
    AllPreviousStepsSucceeded
}
