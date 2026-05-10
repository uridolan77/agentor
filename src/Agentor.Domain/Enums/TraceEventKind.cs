namespace Agentor.Domain.Enums;

public enum TraceEventKind
{
    RunStarted,
    StepStarted,
    PolicyEvaluated,
    ToolCallStarted,
    ToolCallCompleted,
    StepCompleted,
    RunCompleted,
    RunFailed,

    /// <summary>Run stopped pending human or workflow review (distinct from failure).</summary>
    RunRequiresReview,

    /// <summary>Human governance review decision recorded (Agentor-side; does not canonize knowledge).</summary>
    HumanReviewDecisionRecorded,

    /// <summary>Run execution reopened after explicit human approval of a pending reviewed tool.</summary>
    RunResumedAfterHumanReview,

    SessionMemoryWriteAccepted,
    SessionMemoryWriteRejected,

    /// <summary>A single tool execution attempt began (timeout/retry pipeline).</summary>
    ToolExecutionAttemptStarted,

    /// <summary>A single tool execution attempt ended (success, executor failure, timeout, or cancel).</summary>
    ToolExecutionAttemptFinished,

    /// <summary>Scheduling another attempt after a retryable failure.</summary>
    ToolCallRetrying,

    /// <summary>All attempts exhausted with timeout.</summary>
    ToolCallTimedOut,

    /// <summary>Execution stopped due to cancellation.</summary>
    ToolCallCanceled,

    PlanExecutionStarted,
    PlanExecutionStepStarted,
    PlanExecutionStepCompleted,
    PlanExecutionCompleted,
    PlanExecutionFailed,
    PlanExecutionRequiresReview,
    PlanStepSkipped,
    StepGuardEvaluated,
    PlanFailureDecisionRecorded,
    CompensationHookRecorded,

    SkillInvocationStarted,
    SkillInvocationCompleted,
    SkillProcedureSegmentRecorded,

    /// <summary>Run recorded Athanor evidence search identifiers as provenance input (not canon).</summary>
    AthanorEvidenceSearchProvenanceAttached,

    /// <summary>Candidate knowledge was submitted to Athanor for review (not canon).</summary>
    AthanorCandidateSubmitted,

    /// <summary>A review queue item was created in Athanor for human review (not canon).</summary>
    AthanorReviewQueued,

    ExternalAgentCapabilityDiscovered,
    ExternalAgentInvocationStarted,
    ExternalAgentInvocationCompleted,
    ExternalAgentInvocationDenied,
    ExternalAgentInvocationRequiresReview,
    ExternalAgentInvocationFailed,

    /// <summary>A multi-step plan resume cursor was recorded when plan execution suspended for human review with remaining steps outstanding.</summary>
    PlanResumeCursorRecorded,

    /// <summary>The plan resume cursor was cleared immediately before resuming remaining plan step execution.</summary>
    PlanResumeCursorCleared,

    /// <summary>Multi-step plan execution resumed after human review approval; remaining steps will execute in order.</summary>
    MultiStepPlanResumed
}
