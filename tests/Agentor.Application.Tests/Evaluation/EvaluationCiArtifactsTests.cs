using Agentor.Application.Evaluation;
using Agentor.Domain.Enums;
using Xunit;

namespace Agentor.Application.Tests.Evaluation;

/// <summary>Phase 32 PR127 — deterministic CI artifact triple (md/json/csv).</summary>
public sealed class EvaluationCiArtifactsTests
{
    [Fact]
    public void Writes_ci_evaluation_artifacts()
    {
        var rows = new List<CoordinationProfileRunRecord>
        {
            CiRow("alpha", AgentRunStatus.Completed, latencyMs: 12),
            CiRow("beta", AgentRunStatus.Completed, latencyMs: 18)
        };

        var aggregate = EvaluationAggregateReportGenerator.FromRows(rows);
        var thresholdJson = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "eval", "evaluation-thresholds.json"));
        var thresholds = EvaluationThresholdSet.Parse(thresholdJson);
        var thresholdEvaluation = EvaluationThresholdEvaluator.Evaluate(aggregate, aggregate, thresholds);

        var configured = Environment.GetEnvironmentVariable("AGENTOR_EVAL_CI_OUT");
        var target = !string.IsNullOrWhiteSpace(configured)
            ? configured!
            : Path.Combine(Path.GetTempPath(), "agentor-eval-ci-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(target);
            EvaluationReportGenerator.WriteCiArtifactFolder(target, rows, aggregate, thresholdEvaluation);

            Assert.True(File.Exists(Path.Combine(target, "evaluation-report.md")));
            Assert.True(File.Exists(Path.Combine(target, "evaluation-report.json")));
            Assert.True(File.Exists(Path.Combine(target, "evaluation-summary.csv")));

            var md = File.ReadAllText(Path.Combine(target, "evaluation-report.md"));
            Assert.Contains("Aggregate metrics", md, StringComparison.Ordinal);
            Assert.Contains("Threshold evaluation", md, StringComparison.Ordinal);

            var csv = File.ReadAllText(Path.Combine(target, "evaluation-summary.csv"));
            Assert.Contains("aggregate,", csv, StringComparison.Ordinal);
        }
        finally
        {
            if (string.IsNullOrWhiteSpace(configured) && Directory.Exists(target))
            {
                Directory.Delete(target, recursive: true);
            }
        }
    }

    private static CoordinationProfileRunRecord CiRow(string fixtureId, AgentRunStatus status, long latencyMs)
    {
        var snap = new RunEvaluationSnapshot(status, 3, 1, 1, 0);
        var metrics = new CoordinationEvaluationMetrics(
            Reliability: status == AgentRunStatus.Completed ? 1 : 0,
            Resolution: 1,
            CostUnits: 0.25m,
            LatencyMs: latencyMs,
            TokenUsageTotal: 0,
            ReviewBurden: 0.05,
            FailureIsolation: 1,
            EscalationRate: 0,
            DiversityOrCollapseSignal: 0.5,
            PolicyDecisionCount: 1,
            DistinctToolKeysUsed: 1,
            TraceEventCount: 3,
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
            PolicyRequiresReviewDecisionCount: 0);
    }
}
