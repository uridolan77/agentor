using Agentor.Domain.Enums;

namespace Agentor.Domain;

public sealed class RunManifest
{
    public RunManifest(
        Guid runId,
        Guid profileId,
        string traceId,
        AgentRunStatus status,
        DateTimeOffset startedAt,
        DateTimeOffset? completedAt,
        int stepCount,
        int toolCallCount,
        int policyDecisionCount,
        int traceEventCount)
    {
        RunId = runId;
        ProfileId = profileId;
        TraceId = traceId;
        Status = status;
        StartedAt = startedAt;
        CompletedAt = completedAt;
        StepCount = stepCount;
        ToolCallCount = toolCallCount;
        PolicyDecisionCount = policyDecisionCount;
        TraceEventCount = traceEventCount;
    }

    public Guid RunId { get; }

    public Guid ProfileId { get; }

    public string TraceId { get; }

    public AgentRunStatus Status { get; }

    public DateTimeOffset StartedAt { get; }

    public DateTimeOffset? CompletedAt { get; }

    public int StepCount { get; }

    public int ToolCallCount { get; }

    public int PolicyDecisionCount { get; }

    public int TraceEventCount { get; }

    public static RunManifest FromRun(AgentRun run)
    {
        var toolCallCount = run.Steps.Sum(step => step.ToolCalls.Count);
        var policyDecisionCount = run.Steps.Sum(step => step.PolicyDecisions.Count);

        return new RunManifest(
            run.Id,
            run.ProfileId,
            run.TraceId,
            run.Status,
            run.StartedAt,
            run.CompletedAt,
            run.Steps.Count,
            toolCallCount,
            policyDecisionCount,
            run.Trace.Count);
    }
}
