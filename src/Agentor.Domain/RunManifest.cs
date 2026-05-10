using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Agentor.Domain.Enums;

namespace Agentor.Domain;

public sealed class RunManifest
{
    public const string CurrentVersion = "1.2";

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
        int modelCallCount,
        long totalModelPromptTokens,
        long totalModelCompletionTokens,
        decimal totalModelEstimatedCostUnits,
        long totalModelLatencyMs,
        string? primaryModelProviderName,
        string? primaryModelId,
        string? primaryPromptProfileRef,
        string? primaryModelProfileRef,
        int externalAgentInvocationCompletedCount,
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
        ModelCallCount = modelCallCount;
        TotalModelPromptTokens = totalModelPromptTokens;
        TotalModelCompletionTokens = totalModelCompletionTokens;
        TotalModelEstimatedCostUnits = totalModelEstimatedCostUnits;
        TotalModelLatencyMs = totalModelLatencyMs;
        PrimaryModelProviderName = primaryModelProviderName;
        PrimaryModelId = primaryModelId;
        PrimaryPromptProfileRef = primaryPromptProfileRef;
        PrimaryModelProfileRef = primaryModelProfileRef;
        ExternalAgentInvocationCompletedCount = externalAgentInvocationCompletedCount;
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

    public int ModelCallCount { get; }

    public long TotalModelPromptTokens { get; }

    public long TotalModelCompletionTokens { get; }

    public decimal TotalModelEstimatedCostUnits { get; }

    public long TotalModelLatencyMs { get; }

    public string? PrimaryModelProviderName { get; }

    public string? PrimaryModelId { get; }

    public string? PrimaryPromptProfileRef { get; }

    public string? PrimaryModelProfileRef { get; }

    public int ExternalAgentInvocationCompletedCount { get; }

    public string ManifestVersion { get; }

    public string ContentHash { get; }

    /// <summary>
    /// Builds a manifest from run state plus model-gateway telemetry supplied by Application (integration mapping stays out of Domain).
    /// </summary>
    public static RunManifest FromRun(AgentRun run, RunManifestModelTelemetry modelTelemetry)
        => FromRun(run, modelTelemetry, RunManifestExternalAgentTelemetry.Empty);

    public static RunManifest FromRun(
        AgentRun run,
        RunManifestModelTelemetry modelTelemetry,
        RunManifestExternalAgentTelemetry externalTelemetry)
    {
        var toolCallCount = run.Steps.Sum(step => step.ToolCalls.Count);
        var policyDecisionCount = run.Steps.Sum(step => step.PolicyDecisions.Count);
        var traceEventCount = run.Trace.OrderBy(e => e.OccurredAt).Count();

        var mt = modelTelemetry;
        var ext = externalTelemetry;

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
            traceEventCount,
            mt.ModelCallCount,
            mt.TotalPromptTokens,
            mt.TotalCompletionTokens,
            mt.TotalEstimatedCostUnits,
            mt.TotalLatencyMs,
            mt.PrimaryProviderName,
            mt.PrimaryModelId,
            mt.PrimaryPromptProfileRef,
            mt.PrimaryModelProfileRef,
            ext.ExternalAgentInvocationCompletedCount);

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
            mt.ModelCallCount,
            mt.TotalPromptTokens,
            mt.TotalCompletionTokens,
            mt.TotalEstimatedCostUnits,
            mt.TotalLatencyMs,
            mt.PrimaryProviderName,
            mt.PrimaryModelId,
            mt.PrimaryPromptProfileRef,
            mt.PrimaryModelProfileRef,
            ext.ExternalAgentInvocationCompletedCount,
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
        int traceEventCount,
        int modelCallCount,
        long totalModelPromptTokens,
        long totalModelCompletionTokens,
        decimal totalModelEstimatedCostUnits,
        long totalModelLatencyMs,
        string? primaryModelProviderName,
        string? primaryModelId,
        string? primaryPromptProfileRef,
        string? primaryModelProfileRef,
        int externalAgentInvocationCompletedCount)
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
            modelCallCount.ToString(),
            totalModelPromptTokens.ToString(CultureInfo.InvariantCulture),
            totalModelCompletionTokens.ToString(CultureInfo.InvariantCulture),
            totalModelEstimatedCostUnits.ToString(CultureInfo.InvariantCulture),
            totalModelLatencyMs.ToString(CultureInfo.InvariantCulture),
            primaryModelProviderName ?? "null",
            primaryModelId ?? "null",
            primaryPromptProfileRef ?? "null",
            primaryModelProfileRef ?? "null",
            externalAgentInvocationCompletedCount.ToString(CultureInfo.InvariantCulture),
            CurrentVersion
        };

        var canonical = string.Join(":", parts);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
