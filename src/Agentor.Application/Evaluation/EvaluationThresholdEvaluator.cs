using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agentor.Application.Evaluation;

public sealed record EvaluationThresholdSet(
    int SchemaVersion,
    double? MaxFailureRateIncrease,
    double? MaxReviewBurdenIncrease,
    double? MaxLatencyIncreasePercent,
    double? MaxCostIncreasePercent,
    double? MaxQualityViolationIncrease,
    double? MaxPolicyDenyRateIncrease,
    double? MaxRequiresReviewPolicyRateIncrease)
{
    public static EvaluationThresholdSet Parse(string json)
    {
        var dto = JsonSerializer.Deserialize<ThresholdDto>(json, ThresholdJsonOptions)
                  ?? throw new InvalidDataException("Threshold JSON deserialized to null.");

        if (dto.SchemaVersion < 1)
        {
            throw new InvalidDataException("thresholds schemaVersion must be >= 1.");
        }

        return new EvaluationThresholdSet(
            dto.SchemaVersion,
            dto.MaxFailureRateIncrease,
            dto.MaxReviewBurdenIncrease,
            dto.MaxLatencyIncreasePercent,
            dto.MaxCostIncreasePercent,
            dto.MaxQualityViolationIncrease,
            dto.MaxPolicyDenyRateIncrease,
            dto.MaxRequiresReviewPolicyRateIncrease);
    }

    private sealed class ThresholdDto
    {
        public int SchemaVersion { get; set; }
        public double? MaxFailureRateIncrease { get; set; }
        public double? MaxReviewBurdenIncrease { get; set; }
        public double? MaxLatencyIncreasePercent { get; set; }
        public double? MaxCostIncreasePercent { get; set; }
        public double? MaxQualityViolationIncrease { get; set; }
        public double? MaxPolicyDenyRateIncrease { get; set; }
        public double? MaxRequiresReviewPolicyRateIncrease { get; set; }
    }

    private static readonly JsonSerializerOptions ThresholdJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };
}

public enum EvaluationThresholdVerdict
{
    Pass,
    Warn,
    Fail
}

public sealed record EvaluationThresholdFinding(
    string ReasonCode,
    EvaluationThresholdVerdict Severity,
    string Message);

public sealed record EvaluationThresholdResult(
    EvaluationThresholdVerdict Verdict,
    IReadOnlyList<EvaluationThresholdFinding> Findings);

/// <summary>
/// Compares aggregate reports against regression ceilings (PR126).
/// </summary>
public static class EvaluationThresholdEvaluator
{
    private const double DefaultWarnRatio = 0.8;

    public static EvaluationThresholdResult Evaluate(
        EvaluationAggregateReport baseline,
        EvaluationAggregateReport current,
        EvaluationThresholdSet thresholds)
    {
        ArgumentNullException.ThrowIfNull(thresholds);
        var findings = new List<EvaluationThresholdFinding>();

        void Consider(
            string reasonCode,
            double? maxIncrease,
            double delta,
            string label)
        {
            if (maxIncrease is null || maxIncrease.Value <= 0)
            {
                return;
            }

            var abs = delta;
            var fail = abs > maxIncrease.Value;
            var warnThreshold = maxIncrease.Value * DefaultWarnRatio;
            var warn = !fail && abs > warnThreshold;

            if (fail)
            {
                findings.Add(new EvaluationThresholdFinding(
                    reasonCode,
                    EvaluationThresholdVerdict.Fail,
                    $"{label} regression: delta {Format(delta)} exceeds fail ceiling {Format(maxIncrease.Value)}."));
            }
            else if (warn)
            {
                findings.Add(new EvaluationThresholdFinding(
                    reasonCode,
                    EvaluationThresholdVerdict.Warn,
                    $"{label} drift: delta {Format(delta)} exceeds warn band ({Format(warnThreshold)})."));
            }
        }

        Consider(
            "EVAL_THRESHOLD_FAILURE_RATE",
            thresholds.MaxFailureRateIncrease,
            current.FailureRate - baseline.FailureRate,
            "Failure rate");

        Consider(
            "EVAL_THRESHOLD_REVIEW_BURDEN",
            thresholds.MaxReviewBurdenIncrease,
            current.MeanReviewBurden - baseline.MeanReviewBurden,
            "Mean review burden");

        var latencyPct = PercentIncrease(baseline.MeanLatencyMs, current.MeanLatencyMs);
        Consider(
            "EVAL_THRESHOLD_LATENCY_PCT",
            thresholds.MaxLatencyIncreasePercent,
            latencyPct,
            "Mean latency %");

        var costPct = PercentIncrease((double)baseline.MeanCostUnits, (double)current.MeanCostUnits);
        Consider(
            "EVAL_THRESHOLD_COST_PCT",
            thresholds.MaxCostIncreasePercent,
            costPct,
            "Mean cost %");

        Consider(
            "EVAL_THRESHOLD_QUALITY_VIOLATIONS",
            thresholds.MaxQualityViolationIncrease,
            current.MeanQualityViolationsPerRun - baseline.MeanQualityViolationsPerRun,
            "Mean quality violations per run");

        Consider(
            "EVAL_THRESHOLD_POLICY_DENY_RATE",
            thresholds.MaxPolicyDenyRateIncrease,
            current.PolicyDenyRate - baseline.PolicyDenyRate,
            "Policy deny rate");

        Consider(
            "EVAL_THRESHOLD_REQUIRES_REVIEW_RATE",
            thresholds.MaxRequiresReviewPolicyRateIncrease,
            current.RequiresReviewPolicyRate - baseline.RequiresReviewPolicyRate,
            "Requires-review policy rate");

        var orderedFindings = findings
            .OrderBy(f => f.ReasonCode, StringComparer.Ordinal)
            .ToList();

        var verdict = EvaluationThresholdVerdict.Pass;
        if (orderedFindings.Any(f => f.Severity == EvaluationThresholdVerdict.Fail))
        {
            verdict = EvaluationThresholdVerdict.Fail;
        }
        else if (orderedFindings.Any(f => f.Severity == EvaluationThresholdVerdict.Warn))
        {
            verdict = EvaluationThresholdVerdict.Warn;
        }

        return new EvaluationThresholdResult(verdict, orderedFindings);
    }

    private static double PercentIncrease(double baseline, double current)
    {
        if (baseline <= 0)
        {
            return current > 0 ? double.PositiveInfinity : 0;
        }

        return (current - baseline) / baseline * 100.0;
    }

    private static string Format(double v) => v.ToString("0.######", CultureInfo.InvariantCulture);
}
