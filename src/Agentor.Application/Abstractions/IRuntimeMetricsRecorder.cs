namespace Agentor.Application.Abstractions;

/// <summary>
/// In-process runtime metrics (System.Diagnostics.Metrics). Implementations must use safe dimensions only
/// (toolKey, policy effect, integration name, status) — never user payloads or objectives.
/// </summary>
public interface IRuntimeMetricsRecorder
{
    void RecordRunStarted();

    void RecordRunCompleted();

    void RecordRunFailed();

    void RecordRunRequiresReview();

    void RecordPolicyAllowed();

    void RecordPolicyDenied();

    void RecordPolicyRequiresReview();

    void RecordToolStarted(string toolKey);

    void RecordToolCompleted(string toolKey);

    void RecordToolFailed(string toolKey);

    void RecordQueueClaimed();

    void RecordQueueCompleted();

    void RecordQueueFailed();

    void RecordOutboxDispatchStarted();

    void RecordOutboxDispatchCompleted();

    void RecordOutboxDispatchFailed();

    void RecordIntegrationError(string integrationName);
}
