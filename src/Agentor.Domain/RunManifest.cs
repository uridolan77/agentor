using System.Security.Cryptography;
using System.Text;
using Agentor.Domain.Enums;

namespace Agentor.Domain;

public sealed class RunManifest
{
    public const string CurrentVersion = "1.0";

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
        int traceEventCount,
        string manifestVersion,
        string contentHash)
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
        ManifestVersion = manifestVersion;
        ContentHash = contentHash;
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

    public string ManifestVersion { get; }

    public string ContentHash { get; }

    public static RunManifest FromRun(AgentRun run)
    {
        var toolCallCount = run.Steps.Sum(step => step.ToolCalls.Count);
        var policyDecisionCount = run.Steps.Sum(step => step.PolicyDecisions.Count);
        var traceEventCount = run.Trace.OrderBy(e => e.OccurredAt).Count();

        var hash = ComputeContentHash(
            run.Id,
            run.ProfileId,
            run.TraceId,
            run.Status,
            run.StartedAt,
            run.CompletedAt,
            run.Steps.Count,
            toolCallCount,
            policyDecisionCount,
            traceEventCount);

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
            traceEventCount,
            CurrentVersion,
            hash);
    }

    public static string ComputeContentHash(
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
        var parts = new[]
        {
            runId.ToString("D"),
            profileId.ToString("D"),
            traceId,
            status.ToString(),
            startedAt.ToString("O"),
            completedAt?.ToString("O") ?? "null",
            stepCount.ToString(),
            toolCallCount.ToString(),
            policyDecisionCount.ToString(),
            traceEventCount.ToString(),
            CurrentVersion
        };

        var canonical = string.Join(":", parts);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
