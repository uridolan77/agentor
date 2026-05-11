using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Agentor.Domain;
using Agentor.Domain.Enums;

namespace Agentor.Application.Evaluation;

/// <summary>
/// Comparable coordination metrics for baseline/delta analysis (PR124).
/// </summary>
public sealed record EvaluationMetricSnapshot(
    AgentRunStatus RunStatus,
    int TotalQualityViolationCount,
    int TotalQualityWarningCount,
    int TraceEventCount,
    double ReviewBurden,
    long LatencyMs,
    decimal CostUnits,
    int PolicyDecisionCount,
    int PolicyDenyDecisionCount,
    int PolicyRequiresReviewDecisionCount,
    int ExternalAgentInvocationCompletedCount)
{
    public static EvaluationMetricSnapshot FromProfileRow(
        CoordinationProfileRunRecord row,
        AgentRun? run)
    {
        var rv = row.RuntimeQualityViolations.Count;
        var dv = row.DeclarativeViolations.Count;
        var warns = row.RuntimeQualityWarnings.Count;

        var deny = 0;
        var req = 0;
        if (run is not null)
        {
            foreach (var step in run.Steps)
            {
                foreach (var d in step.PolicyDecisions)
                {
                    if (d.Outcome == PolicyDecisionOutcome.Deny)
                    {
                        deny++;
                    }
                    else if (d.Outcome == PolicyDecisionOutcome.RequiresReview)
                    {
                        req++;
                    }
                }
            }
        }

        return new EvaluationMetricSnapshot(
            row.Snapshot.RunStatus,
            rv + dv,
            warns,
            row.Snapshot.TraceEventCount,
            row.Metrics.ReviewBurden,
            row.Metrics.LatencyMs,
            row.Metrics.CostUnits,
            row.Metrics.PolicyDecisionCount,
            deny,
            req,
            row.Snapshot.ExternalAgentInvocationCompletedCount);
    }

    public static string SerializeToStableJson(EvaluationMetricSnapshot snapshot)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("runStatus", snapshot.RunStatus.ToString());
            writer.WriteNumber("totalQualityViolationCount", snapshot.TotalQualityViolationCount);
            writer.WriteNumber("totalQualityWarningCount", snapshot.TotalQualityWarningCount);
            writer.WriteNumber("traceEventCount", snapshot.TraceEventCount);
            writer.WriteNumber("reviewBurden", snapshot.ReviewBurden);
            writer.WriteNumber("latencyMs", snapshot.LatencyMs);
            writer.WriteNumber("costUnits", snapshot.CostUnits);
            writer.WriteNumber("policyDecisionCount", snapshot.PolicyDecisionCount);
            writer.WriteNumber("policyDenyDecisionCount", snapshot.PolicyDenyDecisionCount);
            writer.WriteNumber("policyRequiresReviewDecisionCount", snapshot.PolicyRequiresReviewDecisionCount);
            writer.WriteNumber("externalAgentInvocationCompletedCount", snapshot.ExternalAgentInvocationCompletedCount);
            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }
}

/// <summary>
/// Stored reference snapshot for regression comparison (PR124).
/// </summary>
public sealed record EvaluationBaseline(string Id, EvaluationMetricSnapshot Snapshot)
{
    public string ToStableJson() =>
        JsonSerializer.Serialize(
            new { schemaVersion = 1, id = Id, snapshot = Snapshot },
            BaselineJsonOptions);

    public static EvaluationBaseline FromStableJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var id = root.GetProperty("id").GetString() ?? "";
        var s = root.GetProperty("snapshot");
        var snap = new EvaluationMetricSnapshot(
            Enum.Parse<AgentRunStatus>(s.GetProperty("runStatus").GetString()!, true),
            s.GetProperty("totalQualityViolationCount").GetInt32(),
            s.GetProperty("totalQualityWarningCount").GetInt32(),
            s.GetProperty("traceEventCount").GetInt32(),
            s.GetProperty("reviewBurden").GetDouble(),
            s.GetProperty("latencyMs").GetInt64(),
            s.GetProperty("costUnits").GetDecimal(),
            s.GetProperty("policyDecisionCount").GetInt32(),
            s.GetProperty("policyDenyDecisionCount").GetInt32(),
            s.GetProperty("policyRequiresReviewDecisionCount").GetInt32(),
            s.GetProperty("externalAgentInvocationCompletedCount").GetInt32());
        return new EvaluationBaseline(id, snap);
    }

    private static readonly JsonSerializerOptions BaselineJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };
}

public enum EvaluationDeltaKind
{
    Neutral,
    Improved,
    Regressed
}

public sealed record EvaluationDeltaItem(
    string MetricCode,
    EvaluationDeltaKind Kind,
    string BaselineText,
    string CurrentText,
    string DeltaText);

public sealed record EvaluationDeltaReport(
    IReadOnlyList<EvaluationDeltaItem> Items);

/// <summary>
/// Compares metric snapshots for regression analysis (PR124).
/// </summary>
public static class EvaluationDeltaCalculator
{
    public static EvaluationDeltaReport Compare(EvaluationMetricSnapshot baseline, EvaluationMetricSnapshot current)
    {
        var items = new List<EvaluationDeltaItem>
        {
            CompareLong("DELTA_LATENCY_MS", baseline.LatencyMs, current.LatencyMs, lowerIsBetter: true),
            CompareDecimal("DELTA_COST_UNITS", baseline.CostUnits, current.CostUnits, lowerIsBetter: true),
            CompareInt("DELTA_QUALITY_VIOLATIONS", baseline.TotalQualityViolationCount, current.TotalQualityViolationCount, lowerIsBetter: true),
            CompareInt("DELTA_WARNINGS", baseline.TotalQualityWarningCount, current.TotalQualityWarningCount, lowerIsBetter: true),
            CompareInt("DELTA_TRACE_EVENTS", baseline.TraceEventCount, current.TraceEventCount, lowerIsBetter: false),
            CompareDouble("DELTA_REVIEW_BURDEN", baseline.ReviewBurden, current.ReviewBurden, lowerIsBetter: true),
            CompareInt("DELTA_POLICY_DENIES", baseline.PolicyDenyDecisionCount, current.PolicyDenyDecisionCount, lowerIsBetter: true),
            CompareInt("DELTA_POLICY_REQUIRES_REVIEW", baseline.PolicyRequiresReviewDecisionCount, current.PolicyRequiresReviewDecisionCount, lowerIsBetter: true),
            CompareInt("DELTA_EXTERNAL_AGENT_INVOCATIONS", baseline.ExternalAgentInvocationCompletedCount, current.ExternalAgentInvocationCompletedCount, lowerIsBetter: false),
            CompareStatus("DELTA_RUN_STATUS", baseline.RunStatus, current.RunStatus)
        };

        return new EvaluationDeltaReport(items);
    }

    public static string SerializeDeltaReportToStableJson(EvaluationDeltaReport report)
    {
        var arr = report.Items
            .OrderBy(i => i.MetricCode, StringComparer.Ordinal)
            .Select(i => new
            {
                i.MetricCode,
                kind = i.Kind.ToString(),
                i.BaselineText,
                i.CurrentText,
                i.DeltaText
            })
            .ToList();

        return JsonSerializer.Serialize(
            new { schemaVersion = 1, items = arr },
            new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    private static EvaluationDeltaItem CompareLong(string code, long a, long b, bool lowerIsBetter)
    {
        var kind = KindForNumeric((double)a, (double)b, lowerIsBetter);
        return new EvaluationDeltaItem(code, kind, a.ToString(CultureInfo.InvariantCulture), b.ToString(CultureInfo.InvariantCulture), (b - a).ToString(CultureInfo.InvariantCulture));
    }

    private static EvaluationDeltaItem CompareInt(string code, int a, int b, bool lowerIsBetter)
    {
        var kind = KindForNumeric((double)a, (double)b, lowerIsBetter);
        return new EvaluationDeltaItem(code, kind, a.ToString(CultureInfo.InvariantCulture), b.ToString(CultureInfo.InvariantCulture), (b - a).ToString(CultureInfo.InvariantCulture));
    }

    private static EvaluationDeltaItem CompareDecimal(string code, decimal a, decimal b, bool lowerIsBetter)
    {
        var kind = KindForNumeric((double)a, (double)b, lowerIsBetter);
        return new EvaluationDeltaItem(
            code,
            kind,
            a.ToString(CultureInfo.InvariantCulture),
            b.ToString(CultureInfo.InvariantCulture),
            (b - a).ToString(CultureInfo.InvariantCulture));
    }

    private static EvaluationDeltaItem CompareDouble(string code, double a, double b, bool lowerIsBetter)
    {
        var kind = KindForNumeric(a, b, lowerIsBetter);
        return new EvaluationDeltaItem(
            code,
            kind,
            a.ToString("0.######", CultureInfo.InvariantCulture),
            b.ToString("0.######", CultureInfo.InvariantCulture),
            (b - a).ToString("0.######", CultureInfo.InvariantCulture));
    }

    private static EvaluationDeltaKind KindForNumeric(double baseline, double current, bool lowerIsBetter)
    {
        if (baseline.Equals(current))
        {
            return EvaluationDeltaKind.Neutral;
        }

        var improved = lowerIsBetter ? current < baseline : current > baseline;
        return improved ? EvaluationDeltaKind.Improved : EvaluationDeltaKind.Regressed;
    }

    private static EvaluationDeltaItem CompareStatus(string code, AgentRunStatus a, AgentRunStatus b)
    {
        var kind = Rank(a).CompareTo(Rank(b)) switch
        {
            0 => EvaluationDeltaKind.Neutral,
            > 0 => EvaluationDeltaKind.Improved,
            _ => EvaluationDeltaKind.Regressed
        };
        var deltaRank = Rank(b) - Rank(a);
        return new EvaluationDeltaItem(code, kind, a.ToString(), b.ToString(), deltaRank.ToString(CultureInfo.InvariantCulture));

        static int Rank(AgentRunStatus s) => s switch
        {
            AgentRunStatus.Completed => 3,
            AgentRunStatus.RequiresReview => 2,
            _ => 1
        };
    }
}
