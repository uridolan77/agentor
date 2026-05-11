using Agentor.Domain.Enums;

namespace Agentor.Application.Evaluation;

/// <summary>
/// Cross-run aggregate metrics for comparative evaluation (PR125).
/// </summary>
public sealed record EvaluationAggregateReport(
    int RunCount,
    double MeanLatencyMs,
    double MedianLatencyMs,
    double FailureRate,
    double MeanReviewBurden,
    double PolicyDenyRate,
    double RequiresReviewPolicyRate,
    double MeanExternalAgentInvocations,
    decimal MinCostUnits,
    decimal MaxCostUnits,
    decimal MeanCostUnits,
    double MeanQualityViolationsPerRun,
    double MaxQualityViolationsPerRun);

/// <summary>
/// Deterministic aggregates derived from coordination evaluation rows (PR125).
/// </summary>
public static class EvaluationAggregateReportGenerator
{
    public static EvaluationAggregateReport FromRows(IReadOnlyList<CoordinationProfileRunRecord> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count == 0)
        {
            return new EvaluationAggregateReport(
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0);
        }

        var n = rows.Count;
        var latencies = rows.Select(r => (double)r.Metrics.LatencyMs).OrderBy(x => x).ToList();
        var meanLatency = latencies.Sum() / n;
        var medianLatency = Median(latencies);

        var failures = rows.Count(r => r.Snapshot.RunStatus != AgentRunStatus.Completed);

        var failureRate = failures / (double)n;

        var meanReview = rows.Sum(r => r.Metrics.ReviewBurden) / n;

        var policyTotal = rows.Sum(r => r.Metrics.PolicyDecisionCount);
        var denyTotal = rows.Sum(r => r.PolicyDenyDecisionCount);
        var rrTotal = rows.Sum(r => r.PolicyRequiresReviewDecisionCount);

        var denyRate = policyTotal == 0 ? 0 : denyTotal / (double)policyTotal;
        var rrPolRate = policyTotal == 0 ? 0 : rrTotal / (double)policyTotal;

        var meanExt = rows.Sum(r => (double)r.Snapshot.ExternalAgentInvocationCompletedCount) / n;

        var costs = rows.Select(r => r.Metrics.CostUnits).ToList();
        var minC = costs.Min();
        var maxC = costs.Max();
        var meanC = costs.Sum() / n;

        var viol = rows.Select(r => r.RuntimeQualityViolations.Count + r.DeclarativeViolations.Count).ToList();
        var meanV = viol.Sum() / (double)n;
        var maxV = viol.Max();

        return new EvaluationAggregateReport(
            n,
            meanLatency,
            medianLatency,
            failureRate,
            meanReview,
            denyRate,
            rrPolRate,
            meanExt,
            minC,
            maxC,
            meanC,
            meanV,
            maxV);
    }

    private static double Median(IReadOnlyList<double> sorted)
    {
        if (sorted.Count == 0)
        {
            return 0;
        }

        var mid = sorted.Count / 2;
        if (sorted.Count % 2 == 1)
        {
            return sorted[mid];
        }

        return (sorted[mid - 1] + sorted[mid]) / 2.0;
    }
}
