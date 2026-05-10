using Agentor.Application.Evaluation;
using Agentor.Domain.Enums;
using Xunit;

namespace Agentor.Application.Tests.Evaluation;

public sealed class EvaluationReportGeneratorTests
{
    [Fact]
    public void BuildJson_is_sorted_and_stable()
    {
        var rows = new List<CoordinationProfileRunRecord>
        {
            NewRow("b", CoordinationEvaluationProfile.McpToolBoundPlan, AgentRunStatus.Completed, 3, 1, 0),
            NewRow("a", CoordinationEvaluationProfile.SequentialPipeline, AgentRunStatus.Completed, 5, 1, 0)
        };

        var json = EvaluationReportGenerator.BuildJson(rows);
        var aIndex = json.IndexOf("\"fixtureId\": \"a\"", StringComparison.Ordinal);
        var bIndex = json.IndexOf("\"fixtureId\": \"b\"", StringComparison.Ordinal);
        Assert.True(aIndex > 0 && bIndex > 0);
        Assert.True(aIndex < bIndex, "Rows should be ordered by fixture id then profile.");
        Assert.Contains("1970-01-01T00:00:00Z", json, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteCiArtifactFolder_writes_three_files()
    {
        var dir = Path.Combine(Path.GetTempPath(), "agentor-phase14-report-" + Guid.NewGuid().ToString("N"));
        try
        {
            var rows = new[]
            {
                NewRow("only", CoordinationEvaluationProfile.SequentialPipeline, AgentRunStatus.Completed, 2, 1, 0)
            };
            EvaluationReportGenerator.WriteCiArtifactFolder(dir, rows);
            Assert.True(File.Exists(Path.Combine(dir, "evaluation-report.md")));
            Assert.True(File.Exists(Path.Combine(dir, "evaluation-report.json")));
            Assert.True(File.Exists(Path.Combine(dir, "evaluation-summary.csv")));
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    private static CoordinationProfileRunRecord NewRow(
        string fixtureId,
        CoordinationEvaluationProfile profile,
        AgentRunStatus status,
        int traces,
        int tools,
        int ext)
    {
        var snap = new RunEvaluationSnapshot(status, traces, tools, 1, ext);
        var metrics = new CoordinationEvaluationMetrics(
            Reliability: 1,
            Resolution: 1,
            CostUnits: 0m,
            LatencyMs: 0,
            TokenUsageTotal: 0,
            ReviewBurden: 0,
            FailureIsolation: 1,
            EscalationRate: 0,
            DiversityOrCollapseSignal: 0.5,
            PolicyDecisionCount: 0,
            DistinctToolKeysUsed: tools,
            TraceEventCount: traces,
            ToolCallCount: tools,
            ModelCallCount: 0,
            ExternalAgentInvocationCompletedCount: ext);
        return new CoordinationProfileRunRecord(
            fixtureId,
            profile,
            snap,
            metrics,
            RuntimeQualityPassed: true,
            RuntimeQualityViolations: [],
            RuntimeQualityWarnings: [],
            DeclarativeQualityPassed: true,
            DeclarativeViolations: []);
    }
}
