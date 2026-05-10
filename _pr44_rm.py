import pathlib
ROOT = pathlib.Path(r"c:/dev/agentor")
p = ROOT / "src/Agentor.Domain/RunManifest.cs"
rm = p.read_text(encoding="utf-8")
rm = rm.replace('public const string CurrentVersion = "1.1";', 'public const string CurrentVersion = "1.2";')
rm = rm.replace(
    """        string? primaryPromptProfileRef,
        string? primaryModelProfileRef,
        string manifestVersion,
        string contentHash)
    {""",
    """        string? primaryPromptProfileRef,
        string? primaryModelProfileRef,
        int externalAgentInvocationCompletedCount,
        string manifestVersion,
        string contentHash)
    {""",
)
rm = rm.replace(
    """        PrimaryPromptProfileRef = primaryPromptProfileRef;
        PrimaryModelProfileRef = primaryModelProfileRef;
        ManifestVersion = manifestVersion;
        ContentHash = contentHash;
    }
""",
    """        PrimaryPromptProfileRef = primaryPromptProfileRef;
        PrimaryModelProfileRef = primaryModelProfileRef;
        ExternalAgentInvocationCompletedCount = externalAgentInvocationCompletedCount;
        ManifestVersion = manifestVersion;
        ContentHash = contentHash;
    }
""",
)
rm = rm.replace(
    """    public string? PrimaryModelProfileRef { get; }

    public string ManifestVersion { get; }
""",
    """    public string? PrimaryModelProfileRef { get; }

    public int ExternalAgentInvocationCompletedCount { get; }

    public string ManifestVersion { get; }
""",
)

old_from = """    public static RunManifest FromRun(AgentRun run, RunManifestModelTelemetry modelTelemetry)
    {
        var toolCallCount = run.Steps.Sum(step => step.ToolCalls.Count);
        var policyDecisionCount = run.Steps.Sum(step => step.PolicyDecisions.Count);
        var traceEventCount = run.Trace.OrderBy(e => e.OccurredAt).Count();

        var mt = modelTelemetry;

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
            mt.PrimaryModelProfileRef);

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
            CurrentVersion,
            hash);
    }
"""

new_from = """    public static RunManifest FromRun(AgentRun run, RunManifestModelTelemetry modelTelemetry)
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
"""

if old_from not in rm:
    raise SystemExit("FromRun block not found")
rm = rm.replace(old_from, new_from)

old_hash_sig = """    public static string ComputeContentHash(
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
"""

new_hash_sig = """    public static string ComputeContentHash(
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
"""

if old_hash_sig not in rm:
    raise SystemExit("hash sig not found")
rm = rm.replace(old_hash_sig, new_hash_sig)

p.write_text(rm, encoding="utf-8")
print("RunManifest patched")
