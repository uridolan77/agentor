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
    IReadOnlyList<QualityViolation> DeclarativeViolations);

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

    public static void WriteCiArtifactFolder(string directory, IReadOnlyList<CoordinationProfileRunRecord> rows)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(rows);

        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "evaluation-report.md"), BuildMarkdown(rows), Utf8NoBom);
        File.WriteAllText(Path.Combine(directory, "evaluation-report.json"), BuildJson(rows), Utf8NoBom);
        File.WriteAllText(Path.Combine(directory, "evaluation-summary.csv"), BuildCsv(rows), Utf8NoBom);
    }

    private static readonly UTF8Encoding Utf8NoBom = new(false);

    public static string BuildMarkdown(IReadOnlyList<CoordinationProfileRunRecord> rows)
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

        return sb.ToString();
    }

    public static string BuildJson(IReadOnlyList<CoordinationProfileRunRecord> rows)
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
                declarativeViolations = r.DeclarativeViolations.Select(v => new { v.RuleId, v.Code, v.Message }).ToList()
            })
            .ToList();

        var root = JsonSerializer.SerializeToNode(new { generatedAtUtc = "1970-01-01T00:00:00Z", rows = ordered }, JsonOptions);
        JsonRedaction.Apply(root, RedactionPolicy.CatalogDefault);
        return root!.ToJsonString(JsonOptions);
    }

    public static string BuildCsv(IReadOnlyList<CoordinationProfileRunRecord> rows)
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
