namespace Agentor.Domain.Enums;

public enum FailureHandlingPolicy
{
    FailFast,
    ContinueOnFailure,
    SkipRemaining,
    EscalateToReview,
    RetryViaToolPipelineOnly,
    MarkForCompensation
}
