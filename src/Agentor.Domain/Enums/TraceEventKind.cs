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
    ExternalAgentInvocationFailed
}
