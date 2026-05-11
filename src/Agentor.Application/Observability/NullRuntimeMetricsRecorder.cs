using Agentor.Application.Abstractions;

namespace Agentor.Application.Observability;

public sealed class NullRuntimeMetricsRecorder : IRuntimeMetricsRecorder
{
    public static readonly NullRuntimeMetricsRecorder Instance = new();

    private NullRuntimeMetricsRecorder()
    {
    }

    public void RecordRunStarted()
    {
    }

    public void RecordRunCompleted()
    {
    }

    public void RecordRunFailed()
    {
    }

    public void RecordRunRequiresReview()
    {
    }

    public void RecordPolicyAllowed()
    {
    }

    public void RecordPolicyDenied()
    {
    }

    public void RecordPolicyRequiresReview()
    {
    }

    public void RecordToolStarted(string toolKey)
    {
    }

    public void RecordToolCompleted(string toolKey)
    {
    }

    public void RecordToolFailed(string toolKey)
    {
    }

    public void RecordQueueClaimed()
    {
    }

    public void RecordQueueCompleted()
    {
    }

    public void RecordQueueFailed()
    {
    }

    public void RecordOutboxDispatchStarted()
    {
    }

    public void RecordOutboxDispatchCompleted()
    {
    }

    public void RecordOutboxDispatchFailed()
    {
    }

    public void RecordIntegrationError(string integrationName)
    {
    }
}
