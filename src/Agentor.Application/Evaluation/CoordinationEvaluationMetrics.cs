using Agentor.Domain;
using Agentor.Domain.Enums;

namespace Agentor.Application.Evaluation;

/// <summary>
/// Coordination-oriented metrics derived only from <see cref="AgentRun"/> and optional <see cref="RunManifest"/> aggregates (PR69).
/// </summary>
public sealed record CoordinationEvaluationMetrics(
    double Reliability,
    double Resolution,
    decimal CostUnits,
    long LatencyMs,
    long TokenUsageTotal,
    double ReviewBurden,
    double FailureIsolation,
    double EscalationRate,
    double DiversityOrCollapseSignal,
    int PolicyDecisionCount,
    int DistinctToolKeysUsed,
    int TraceEventCount,
    int ToolCallCount,
    int ModelCallCount,
    int ExternalAgentInvocationCompletedCount)
{
    public static CoordinationEvaluationMetrics FromArtifacts(
        AgentRun run,
        RunManifest? manifest = null)
    {
        var toolCalls = run.Steps.Sum(s => s.ToolCalls.Count);
        var policyDecisionCount = run.Steps.Sum(s => s.PolicyDecisions.Count);
        var distinctTools = run.Steps
            .SelectMany(s => s.ToolCalls)
            .Select(c => c.ToolKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        var traceKinds = run.Trace.Select(e => e.Kind).Distinct().Count();
        var diversity = traceKinds == 0
            ? 0
            : Math.Clamp((double)distinctTools / traceKinds, 0, 1);

        var requiresReviewPolicies = run.Steps.Sum(s =>
            s.PolicyDecisions.Count(d => d.Outcome == PolicyDecisionOutcome.RequiresReview));

        var reviewBurden = toolCalls == 0
            ? (run.Status == AgentRunStatus.RequiresReview ? 1 : 0)
            : Math.Clamp((requiresReviewPolicies + (run.Status == AgentRunStatus.RequiresReview ? 1 : 0)) / (double)toolCalls, 0, 1);

        var planFailedSteps = run.Status == AgentRunStatus.Completed
            ? run.Steps.Count(s => s.Status == AgentStepStatus.Failed)
            : run.Steps.Count(s => s.Status == AgentStepStatus.Failed);
        var failureIsolation = run.Steps.Count == 0
            ? 1
            : 1 - Math.Clamp((double)planFailedSteps / run.Steps.Count, 0, 1);

        var escalationTraces = run.Trace.Count(e =>
            e.Kind is TraceEventKind.PlanExecutionRequiresReview
                or TraceEventKind.ExternalAgentInvocationRequiresReview);
        var escalationRate = toolCalls == 0 ? 0 : Math.Clamp(escalationTraces / (double)toolCalls, 0, 1);

        var reliability = run.Status == AgentRunStatus.Completed ? 1.0 : 0.0;
        var resolution = run.Status switch
        {
            AgentRunStatus.Completed => 1.0,
            AgentRunStatus.RequiresReview => 0.5,
            _ => 0.0
        };

        long latencyMs = 0;
        var endTime = run.CompletedAt ?? run.TerminalAt;
        if (endTime is { } end)
        {
            latencyMs = (long)Math.Max(0, (end - run.StartedAt).TotalMilliseconds);
        }

        var cost = manifest?.TotalModelEstimatedCostUnits ?? 0m;
        var tokens = manifest is null ? 0L : manifest.TotalModelPromptTokens + manifest.TotalModelCompletionTokens;
        var modelCalls = manifest?.ModelCallCount ?? 0;
        var ext = manifest?.ExternalAgentInvocationCompletedCount
                  ?? run.Trace.Count(e => e.Kind == TraceEventKind.ExternalAgentInvocationCompleted);

        return new CoordinationEvaluationMetrics(
            reliability,
            resolution,
            cost,
            latencyMs,
            tokens,
            reviewBurden,
            failureIsolation,
            escalationRate,
            diversity,
            policyDecisionCount,
            distinctTools,
            run.Trace.Count,
            toolCalls,
            modelCalls,
            ext);
    }
}
