using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Agentor.Domain.Enums;

namespace Agentor.Domain;

public sealed class RunManifest
{
    public const string CurrentVersion = "1.1";

    /// <summary>Must match Application <c>WellKnownToolKeys.ConexusModelComplete</c> for manifest aggregation.</summary>
    private const string ConexusModelCompleteToolKey = "conexus.model-complete";

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

    public string ManifestVersion { get; }

    public string ContentHash { get; }

    public static RunManifest FromRun(AgentRun run)
    {
        var toolCallCount = run.Steps.Sum(step => step.ToolCalls.Count);
        var policyDecisionCount = run.Steps.Sum(step => step.PolicyDecisions.Count);
        var traceEventCount = run.Trace.OrderBy(e => e.OccurredAt).Count();

        var mt = SummarizeConexusModelCalls(run);

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
            mt.Count,
            mt.TotalPromptTokens,
            mt.TotalCompletionTokens,
            mt.TotalCostUnits,
            mt.TotalLatencyMs,
            mt.PrimaryProvider,
            mt.PrimaryModelId,
            mt.PromptProfileRef,
            mt.ModelProfileRef);

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
            mt.Count,
            mt.TotalPromptTokens,
            mt.TotalCompletionTokens,
            mt.TotalCostUnits,
            mt.TotalLatencyMs,
            mt.PrimaryProvider,
            mt.PrimaryModelId,
            mt.PromptProfileRef,
            mt.ModelProfileRef,
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
        string? primaryModelProfileRef)
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
            CurrentVersion
        };

        var canonical = string.Join(":", parts);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private readonly record struct ConexusModelManifestSummary(
        int Count,
        long TotalPromptTokens,
        long TotalCompletionTokens,
        decimal TotalCostUnits,
        long TotalLatencyMs,
        string? PrimaryProvider,
        string? PrimaryModelId,
        string? PromptProfileRef,
        string? ModelProfileRef);

    private static ConexusModelManifestSummary SummarizeConexusModelCalls(AgentRun run)
    {
        long pTok = 0;
        long cTok = 0;
        decimal cost = 0;
        long lat = 0;
        var count = 0;
        string? firstProv = null;
        string? firstModel = null;
        string? firstPRef = null;
        string? firstMRef = null;

        foreach (var step in run.Steps)
        {
            foreach (var call in step.ToolCalls)
            {
                if (!string.Equals(call.ToolKey, ConexusModelCompleteToolKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (call.Status != ToolCallStatus.Succeeded)
                {
                    continue;
                }

                count++;
                if (firstProv is null && call.Output.TryGetValue("providerName", out var pn) && !string.IsNullOrWhiteSpace(pn))
                {
                    firstProv = pn.Trim();
                }

                if (firstModel is null && call.Output.TryGetValue("modelId", out var mid) && !string.IsNullOrWhiteSpace(mid))
                {
                    firstModel = mid.Trim();
                }

                if (firstPRef is null && call.Output.TryGetValue("promptProfileRef", out var pr) && !string.IsNullOrWhiteSpace(pr))
                {
                    firstPRef = pr.Trim();
                }

                if (firstMRef is null && call.Output.TryGetValue("modelProfileRef", out var mr) && !string.IsNullOrWhiteSpace(mr))
                {
                    firstMRef = mr.Trim();
                }

                if (call.Output.TryGetValue("promptTokens", out var pts)
                    && long.TryParse(pts, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pv))
                {
                    pTok += pv;
                }

                if (call.Output.TryGetValue("completionTokens", out var cts)
                    && long.TryParse(cts, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cv))
                {
                    cTok += cv;
                }

                if (call.Output.TryGetValue("estimatedCostUnits", out var cu)
                    && decimal.TryParse(cu, NumberStyles.Number, CultureInfo.InvariantCulture, out var cd))
                {
                    cost += cd;
                }

                if (call.Output.TryGetValue("latencyMs", out var lm)
                    && long.TryParse(lm, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lv))
                {
                    lat += lv;
                }
            }
        }

        cost = decimal.Round(cost, 9, MidpointRounding.AwayFromZero);

        return new ConexusModelManifestSummary(
            count,
            pTok,
            cTok,
            cost,
            lat,
            firstProv,
            firstModel,
            firstPRef,
            firstMRef);
    }
}
