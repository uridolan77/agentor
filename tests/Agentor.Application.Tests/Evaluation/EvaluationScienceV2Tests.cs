using System.Text.Json;
using Agentor.Application.Evaluation;
using Agentor.Domain.Enums;
using Xunit;

namespace Agentor.Application.Tests.Evaluation;

/// <summary>Phase 32 — evaluation science v2 (PR123–PR126).</summary>
public sealed class EvaluationScienceV2Tests
{
    [Fact]
    public void EvaluationDatasetRegistry_loads_validates_and_tags_select_subsets()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "fixtures", "eval");
        var fixtures = EvaluationFixtureRegistry.Load(Path.Combine(dir, "registry.json"), dir);
        var regPath = Path.Combine(dir, "evaluation-datasets.json");
        var reg = EvaluationDatasetRegistry.Load(regPath, fixtures);

        Assert.Single(reg.Datasets);
        Assert.Equal("coordination-default", reg.Datasets[0].Id);
        Assert.Equal(3, reg.Datasets[0].Cases.Count);

        var smoke = reg.SelectCases(new HashSet<EvaluationCaseTag> { EvaluationCaseTag.Smoke });
        Assert.Single(smoke);
        Assert.Equal("seq-one-step-smoke", smoke[0].Id);

        var ext = reg.SelectCases(new HashSet<EvaluationCaseTag> { EvaluationCaseTag.ExternalAgent });
        Assert.Single(ext);
        Assert.Equal("external-agent-call", ext[0].Id);

        var reviewQueue = reg.SelectCases(new HashSet<EvaluationCaseTag>
        {
            EvaluationCaseTag.Review,
            EvaluationCaseTag.Queue
        });
        Assert.Single(reviewQueue);
        Assert.Equal("review-gated-sample", reviewQueue[0].Id);
    }

    [Fact]
    public void EvaluationDatasetRegistry_rejects_duplicate_dataset_id()
    {
        var json = """
                   {"schemaVersion":1,"kind":"EvaluationDatasetRegistry","datasets":[
                     {"id":"dup","cases":[{"id":"a","fixtureId":"one-step-fake-tool","profile":"SequentialPipeline","tags":["smoke"]}]},
                     {"id":"dup","cases":[{"id":"b","fixtureId":"one-step-fake-tool","profile":"SequentialPipeline","tags":["smoke"]}]}]}
                   """;
        var dir = Path.Combine(AppContext.BaseDirectory, "fixtures", "eval");
        var fixtures = EvaluationFixtureRegistry.Load(Path.Combine(dir, "registry.json"), dir);
        var path = Path.Combine(Path.GetTempPath(), "ds-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, json);
        try
        {
            Assert.Throws<InvalidDataException>(() => EvaluationDatasetRegistry.Load(path, fixtures));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void EvaluationDatasetRegistry_rejects_unknown_fixture()
    {
        var json = """
                   {"schemaVersion":1,"kind":"EvaluationDatasetRegistry","datasets":[
                     {"id":"x","cases":[{"id":"only","fixtureId":"does-not-exist","profile":"SequentialPipeline","tags":["smoke"]}]}]}
                   """;
        var dir = Path.Combine(AppContext.BaseDirectory, "fixtures", "eval");
        var fixtures = EvaluationFixtureRegistry.Load(Path.Combine(dir, "registry.json"), dir);
        var path = Path.Combine(Path.GetTempPath(), "ds-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, json);
        try
        {
            Assert.Throws<InvalidDataException>(() => EvaluationDatasetRegistry.Load(path, fixtures));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void EvaluationDeltaCalculator_detects_improvement_regression_neutral_latency()
    {
        var baseSnap = new EvaluationMetricSnapshot(
            AgentRunStatus.Completed,
            0,
            0,
            10,
            0.1,
            100,
            1m,
            2,
            0,
            0,
            0);

        var improved = baseSnap with { LatencyMs = 50 };
        var improvedRep = EvaluationDeltaCalculator.Compare(baseSnap, improved);
        Assert.Contains(improvedRep.Items, i => i.MetricCode == "DELTA_LATENCY_MS" && i.Kind == EvaluationDeltaKind.Improved);

        var regressed = baseSnap with { LatencyMs = 200 };
        var regressedRep = EvaluationDeltaCalculator.Compare(baseSnap, regressed);
        Assert.Contains(regressedRep.Items, i => i.MetricCode == "DELTA_LATENCY_MS" && i.Kind == EvaluationDeltaKind.Regressed);

        var neutralRep = EvaluationDeltaCalculator.Compare(baseSnap, baseSnap);
        Assert.All(neutralRep.Items.Where(i => i.MetricCode == "DELTA_LATENCY_MS"), i => Assert.Equal(EvaluationDeltaKind.Neutral, i.Kind));
    }

    [Fact]
    public void EvaluationDelta_serializes_to_stable_sorted_json()
    {
        var a = new EvaluationMetricSnapshot(
            AgentRunStatus.Completed,
            1,
            0,
            5,
            0.2,
            10,
            0m,
            1,
            0,
            1,
            0);
        var b = a with { LatencyMs = 20 };
        var report = EvaluationDeltaCalculator.Compare(a, b);
        var json = EvaluationDeltaCalculator.SerializeDeltaReportToStableJson(report);
        var doc = JsonDocument.Parse(json);
        Assert.Equal(1, doc.RootElement.GetProperty("schemaVersion").GetInt32());
        var items = doc.RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.True(items.Count > 0);
        var codes = items.Select(e => e.GetProperty("metricCode").GetString()).ToList();
        Assert.Equal(codes.OrderBy(x => x, StringComparer.Ordinal).ToList(), codes);
    }

    [Fact]
    public void EvaluationBaseline_round_trips_stable_json()
    {
        var snap = new EvaluationMetricSnapshot(
            AgentRunStatus.RequiresReview,
            2,
            1,
            8,
            0.5,
            99,
            3.5m,
            4,
            1,
            2,
            1);
        var baseline = new EvaluationBaseline("baseline-1", snap);
        var json = baseline.ToStableJson();
        var back = EvaluationBaseline.FromStableJson(json);
        Assert.Equal("baseline-1", back.Id);
        Assert.Equal(snap.RunStatus, back.Snapshot.RunStatus);
        Assert.Equal(snap.TotalQualityViolationCount, back.Snapshot.TotalQualityViolationCount);
        Assert.Equal(snap.PolicyRequiresReviewDecisionCount, back.Snapshot.PolicyRequiresReviewDecisionCount);
    }

    [Fact]
    public void EvaluationAggregateReportGenerator_computes_mean_and_median()
    {
        var rows = new List<CoordinationProfileRunRecord>
        {
            NewRow("a", AgentRunStatus.Completed, latencyMs: 10),
            NewRow("b", AgentRunStatus.Completed, latencyMs: 30)
        };
        var agg = EvaluationAggregateReportGenerator.FromRows(rows);
        Assert.Equal(2, agg.RunCount);
        Assert.Equal(20, agg.MeanLatencyMs);
        Assert.Equal(20, agg.MedianLatencyMs);
        Assert.Equal(0, agg.FailureRate);
    }

    [Fact]
    public void EvaluationThresholdEvaluator_passes_on_identical_aggregates()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "fixtures", "eval");
        var json = File.ReadAllText(Path.Combine(dir, "evaluation-thresholds.json"));
        var thresholds = EvaluationThresholdSet.Parse(json);
        var rows = new List<CoordinationProfileRunRecord> { NewRow("x", AgentRunStatus.Completed, latencyMs: 5) };
        var agg = EvaluationAggregateReportGenerator.FromRows(rows);
        var result = EvaluationThresholdEvaluator.Evaluate(agg, agg, thresholds);
        Assert.Equal(EvaluationThresholdVerdict.Pass, result.Verdict);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void EvaluationThresholdEvaluator_emits_stable_fail_codes_on_regression()
    {
        var thresholds = EvaluationThresholdSet.Parse("""
                                                      {"schemaVersion":1,"maxFailureRateIncrease":0.01}
                                                      """);
        var good = EvaluationAggregateReportGenerator.FromRows(new[]
        {
            NewRow("a", AgentRunStatus.Completed, latencyMs: 1),
            NewRow("b", AgentRunStatus.Completed, latencyMs: 1)
        });
        var bad = EvaluationAggregateReportGenerator.FromRows(new[]
        {
            NewRow("a", AgentRunStatus.Completed, latencyMs: 1),
            NewRow("b", AgentRunStatus.Failed, latencyMs: 1)
        });
        var result = EvaluationThresholdEvaluator.Evaluate(good, bad, thresholds);
        Assert.Equal(EvaluationThresholdVerdict.Fail, result.Verdict);
        Assert.Contains(result.Findings, f => f.ReasonCode == "EVAL_THRESHOLD_FAILURE_RATE");
    }

    private static CoordinationProfileRunRecord NewRow(string fixtureId, AgentRunStatus status, long latencyMs)
    {
        var snap = new RunEvaluationSnapshot(status, 2, 1, 1, 0);
        var metrics = new CoordinationEvaluationMetrics(
            Reliability: status == AgentRunStatus.Completed ? 1 : 0,
            Resolution: 1,
            CostUnits: 0m,
            LatencyMs: latencyMs,
            TokenUsageTotal: 0,
            ReviewBurden: 0.1,
            FailureIsolation: 1,
            EscalationRate: 0,
            DiversityOrCollapseSignal: 0.5,
            PolicyDecisionCount: 2,
            DistinctToolKeysUsed: 1,
            TraceEventCount: 2,
            ToolCallCount: 1,
            ModelCallCount: 0,
            ExternalAgentInvocationCompletedCount: 0);
        return new CoordinationProfileRunRecord(
            fixtureId,
            CoordinationEvaluationProfile.SequentialPipeline,
            snap,
            metrics,
            RuntimeQualityPassed: true,
            RuntimeQualityViolations: [],
            RuntimeQualityWarnings: [],
            DeclarativeQualityPassed: true,
            DeclarativeViolations: [],
            PolicyDenyDecisionCount: 0,
            PolicyRequiresReviewDecisionCount: 1);
    }
}
