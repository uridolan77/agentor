using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Agentor.Application.Redaction;

namespace Agentor.Application.Evaluation;

public sealed record CoordinationProfileRunRecord(
    string FixtureId,
    CoordinationEvaluationProfile Profile,
    RunEvaluationSnapshot Snapshot,
    CoordinationEvaluationMetrics Metrics,
    bool RuntimeQualityPassed,
    IReadOnlyList<string> RuntimeQualityViolations,
    IReadOnlyList<string> RuntimeQualityWarnings,
    bool DeclarativeQualityPassed,
    IReadOnlyList<QualityViolation> DeclarativeViolations,
    int PolicyDenyDecisionCount = 0,
    int PolicyRequiresReviewDecisionCount = 0);

/// <summary>
/// Deterministic Markdown / JSON / CSV outputs for CI artifacts (PR70).
/// </summary>
public static class EvaluationReportGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void WriteCiArtifactFolder(
        string directory,
        IReadOnlyList<CoordinationProfileRunRecord> rows,
        EvaluationAggregateReport? aggregate = null,
        EvaluationThresholdResult? thresholdEvaluation = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(rows);

        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "evaluation-report.md"), BuildMarkdown(rows, aggregate, thresholdEvaluation), Utf8NoBom);
        File.WriteAllText(Path.Combine(directory, "evaluation-report.json"), BuildJson(rows, aggregate, thresholdEvaluation), Utf8NoBom);
        File.WriteAllText(Path.Combine(directory, "evaluation-summary.csv"), BuildCsv(rows, aggregate), Utf8NoBom);
    }

    private static readonly UTF8Encoding Utf8NoBom = new(false);

    public static string BuildMarkdown(
        IReadOnlyList<CoordinationProfileRunRecord> rows,
        EvaluationAggregateReport? aggregate = null,
        EvaluationThresholdResult? thresholdEvaluation = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Agentor coordination evaluation report");
        sb.AppendLine();
        sb.AppendLine("| Fixture | Profile | Run status | Traces | Tools | Ext | Reliability | Cost | Declarative OK |");
        sb.AppendLine("|---|---|---:|---:|---:|---:|---:|---:|---|");
        foreach (var r in rows.OrderBy(x => x.FixtureId, StringComparer.Ordinal).ThenBy(x => x.Profile.ToString(), StringComparer.Ordinal))
        {
            sb.Append('|')
                .Append(EscapeMd(r.FixtureId)).Append('|')
                .Append(r.Profile).Append('|')
                .Append(r.Snapshot.RunStatus).Append('|')
                .Append(r.Snapshot.TraceEventCount).Append('|')
                .Append(r.Snapshot.ToolCallCount).Append('|')
                .Append(r.Snapshot.ExternalAgentInvocationCompletedCount).Append('|')
                .Append(FormatDouble(r.Metrics.Reliability)).Append('|')
                .Append(FormatDecimal(r.Metrics.CostUnits)).Append('|')
                .Append(r.DeclarativeQualityPassed ? "yes" : "no")
                .AppendLine("|");
        }

        sb.AppendLine();
        sb.AppendLine("## Quality notes");
        foreach (var r in rows.OrderBy(x => x.FixtureId, StringComparer.Ordinal).ThenBy(x => x.Profile.ToString(), StringComparer.Ordinal))
        {
            if (r.RuntimeQualityViolations.Count == 0 && r.DeclarativeViolations.Count == 0)
            {
                continue;
            }

            sb.AppendLine($"### {r.FixtureId} / {r.Profile}");
            foreach (var v in r.RuntimeQualityViolations)
            {
                sb.AppendLine($"- runtime violation: `{EscapeMd(v)}`");
            }

            foreach (var v in r.DeclarativeViolations)
            {
                sb.AppendLine($"- rule `{EscapeMd(v.RuleId)}` ({EscapeMd(v.Code)}): {EscapeMd(v.Message)}");
            }
        }

        if (aggregate is { } agg)
        {
            sb.AppendLine();
            sb.AppendLine("## Aggregate metrics");
            sb.AppendLine();
            sb.AppendLine("| Runs | Mean latency (ms) | Median latency (ms) | Failure rate | Mean review burden | Policy deny rate | Req-review policy rate | Mean ext invocations | Cost min | Cost max | Mean cost | Mean qual violations | Max qual violations |");
            sb.AppendLine("|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
            sb.Append('|')
                .Append(agg.RunCount).Append('|')
                .Append(FormatDouble(agg.MeanLatencyMs)).Append('|')
                .Append(FormatDouble(agg.MedianLatencyMs)).Append('|')
                .Append(FormatDouble(agg.FailureRate)).Append('|')
                .Append(FormatDouble(agg.MeanReviewBurden)).Append('|')
                .Append(FormatDouble(agg.PolicyDenyRate)).Append('|')
                .Append(FormatDouble(agg.RequiresReviewPolicyRate)).Append('|')
                .Append(FormatDouble(agg.MeanExternalAgentInvocations)).Append('|')
                .Append(FormatDecimal(agg.MinCostUnits)).Append('|')
                .Append(FormatDecimal(agg.MaxCostUnits)).Append('|')
                .Append(FormatDecimal(agg.MeanCostUnits)).Append('|')
                .Append(FormatDouble(agg.MeanQualityViolationsPerRun)).Append('|')
                .Append(FormatDouble(agg.MaxQualityViolationsPerRun))
                .AppendLine("|");
        }

        if (thresholdEvaluation is { } te)
        {
            sb.AppendLine();
            sb.AppendLine("## Threshold evaluation");
            sb.AppendLine();
            sb.AppendLine($"Verdict: **{te.Verdict}**");
            foreach (var f in te.Findings)
            {
                sb.AppendLine($"- `{EscapeMd(f.ReasonCode)}` ({f.Severity}): {EscapeMd(f.Message)}");
            }
        }

        return sb.ToString();
    }

    public static string BuildJson(
        IReadOnlyList<CoordinationProfileRunRecord> rows,
        EvaluationAggregateReport? aggregate = null,
        EvaluationThresholdResult? thresholdEvaluation = null)
    {
        var ordered = rows
            .OrderBy(x => x.FixtureId, StringComparer.Ordinal)
            .ThenBy(x => x.Profile.ToString(), StringComparer.Ordinal)
            .Select(r => new
            {
                r.FixtureId,
                Profile = r.Profile.ToString(),
                snapshot = new
                {
                    r.Snapshot.RunStatus,
                    r.Snapshot.TraceEventCount,
                    r.Snapshot.ToolCallCount,
                    r.Snapshot.PlanStepCount,
                    r.Snapshot.ExternalAgentInvocationCompletedCount
                },
                metrics = new
                {
                    r.Metrics.Reliability,
                    r.Metrics.Resolution,
                    r.Metrics.CostUnits,
                    r.Metrics.LatencyMs,
                    r.Metrics.TokenUsageTotal,
                    r.Metrics.ReviewBurden,
                    r.Metrics.FailureIsolation,
                    r.Metrics.EscalationRate,
                    r.Metrics.DiversityOrCollapseSignal,
                    r.Metrics.PolicyDecisionCount,
                    r.Metrics.DistinctToolKeysUsed,
                    r.Metrics.TraceEventCount,
                    r.Metrics.ToolCallCount,
                    r.Metrics.ModelCallCount,
                    r.Metrics.ExternalAgentInvocationCompletedCount
                },
                r.RuntimeQualityPassed,
                r.RuntimeQualityViolations,
                r.RuntimeQualityWarnings,
                r.DeclarativeQualityPassed,
                declarativeViolations = r.DeclarativeViolations.Select(v => new { v.RuleId, v.Code, v.Message }).ToList(),
                r.PolicyDenyDecisionCount,
                r.PolicyRequiresReviewDecisionCount
            })
            .ToList();

        var root = JsonSerializer.SerializeToNode(
            new
            {
                generatedAtUtc = "1970-01-01T00:00:00Z",
                rows = ordered,
                aggregate = aggregate is null
                    ? null
                    : new
                    {
                        aggregate!.RunCount,
                        aggregate.MeanLatencyMs,
                        aggregate.MedianLatencyMs,
                        aggregate.FailureRate,
                        aggregate.MeanReviewBurden,
                        aggregate.PolicyDenyRate,
                        aggregate.RequiresReviewPolicyRate,
                        aggregate.MeanExternalAgentInvocations,
                        aggregate.MinCostUnits,
                        aggregate.MaxCostUnits,
                        aggregate.MeanCostUnits,
                        aggregate.MeanQualityViolationsPerRun,
                        aggregate.MaxQualityViolationsPerRun
                    },
                thresholdEvaluation = thresholdEvaluation is null
                    ? null
                    : new
                    {
                        verdict = thresholdEvaluation.Verdict.ToString(),
                        findings = thresholdEvaluation.Findings.Select(f => new { f.ReasonCode, severity = f.Severity.ToString(), f.Message }).ToList()
                    }
            },
            JsonOptions);
        JsonRedaction.Apply(root, RedactionPolicy.CatalogDefault);
        return root!.ToJsonString(JsonOptions);
    }

    public static string BuildCsv(IReadOnlyList<CoordinationProfileRunRecord> rows, EvaluationAggregateReport? aggregate = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("fixtureId,profile,runStatus,traceCount,toolCount,externalCount,reliability,costUnits,declarativeOk");
        foreach (var r in rows.OrderBy(x => x.FixtureId, StringComparer.Ordinal).ThenBy(x => x.Profile.ToString(), StringComparer.Ordinal))
        {
            sb.Append(Csv(r.FixtureId)).Append(',')
                .Append(Csv(r.Profile.ToString())).Append(',')
                .Append(Csv(r.Snapshot.RunStatus.ToString())).Append(',')
                .Append(r.Snapshot.TraceEventCount).Append(',')
                .Append(r.Snapshot.ToolCallCount).Append(',')
                .Append(r.Snapshot.ExternalAgentInvocationCompletedCount).Append(',')
                .Append(FormatDouble(r.Metrics.Reliability)).Append(',')
                .Append(FormatDecimal(r.Metrics.CostUnits)).Append(',')
                .Append(r.DeclarativeQualityPassed ? "true" : "false")
                .AppendLine();
        }

        if (aggregate is { } agg)
        {
            sb.AppendLine();
            sb.AppendLine(
                "section,runCount,meanLatencyMs,medianLatencyMs,failureRate,meanReviewBurden,policyDenyRate,requiresReviewPolicyRate,meanExternalAgentInvocations,minCostUnits,maxCostUnits,meanCostUnits,meanQualityViolationsPerRun,maxQualityViolationsPerRun");
            sb.Append("aggregate,")
                .Append(agg.RunCount).Append(',')
                .Append(FormatDouble(agg.MeanLatencyMs)).Append(',')
                .Append(FormatDouble(agg.MedianLatencyMs)).Append(',')
                .Append(FormatDouble(agg.FailureRate)).Append(',')
                .Append(FormatDouble(agg.MeanReviewBurden)).Append(',')
                .Append(FormatDouble(agg.PolicyDenyRate)).Append(',')
                .Append(FormatDouble(agg.RequiresReviewPolicyRate)).Append(',')
                .Append(FormatDouble(agg.MeanExternalAgentInvocations)).Append(',')
                .Append(FormatDecimal(agg.MinCostUnits)).Append(',')
                .Append(FormatDecimal(agg.MaxCostUnits)).Append(',')
                .Append(FormatDecimal(agg.MeanCostUnits)).Append(',')
                .Append(FormatDouble(agg.MeanQualityViolationsPerRun)).Append(',')
                .Append(FormatDouble(agg.MaxQualityViolationsPerRun))
                .AppendLine();
        }

        return sb.ToString();
    }

    private static string EscapeMd(string? s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return "";
        }

        return s.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("|", "\\|", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
    }

    private static string Csv(string? s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return "";
        }

        if (s.Contains('"') || s.Contains(',') || s.Contains('\n'))
        {
            return "\"" + s.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }

        return s;
    }

    private static string FormatDouble(double v) => v.ToString("0.######", CultureInfo.InvariantCulture);

    private static string FormatDecimal(decimal v) => v.ToString("0.######", CultureInfo.InvariantCulture);
}
