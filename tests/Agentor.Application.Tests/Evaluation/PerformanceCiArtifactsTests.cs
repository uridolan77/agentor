using Agentor.Application.Evaluation;
using Xunit;

namespace Agentor.Application.Tests.Evaluation;

/// <summary>Phase 39 PR162 — deterministic performance baseline triple (md/json/csv).</summary>
public sealed class PerformanceCiArtifactsTests
{
    [Fact]
    public void Writes_ci_performance_artifacts()
    {
        var rows = new List<PerformanceMetricRow>
        {
            new("audit-export", 2.5, 2.4, 1000, "BenchmarkDotNet local"),
            new("single-tool-run", 8.1, 7.9, 1000, "BenchmarkDotNet local"),
        };

        var configured = Environment.GetEnvironmentVariable("AGENTOR_PERF_CI_OUT");
        var target = !string.IsNullOrWhiteSpace(configured)
            ? configured!
            : Path.Combine(Path.GetTempPath(), "agentor-perf-ci-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(target);
            PerformanceReportGenerator.WriteCiArtifactFolder(target, rows);

            Assert.True(File.Exists(Path.Combine(target, "performance-report.md")));
            Assert.True(File.Exists(Path.Combine(target, "performance-report.json")));
            Assert.True(File.Exists(Path.Combine(target, "performance-summary.csv")));

            var md = File.ReadAllText(Path.Combine(target, "performance-report.md"));
            Assert.Contains("single-tool-run", md, StringComparison.Ordinal);
            var json = File.ReadAllText(Path.Combine(target, "performance-report.json"));
            Assert.Contains("\"rows\"", json, StringComparison.Ordinal);
        }
        finally
        {
            if (string.IsNullOrWhiteSpace(configured) && Directory.Exists(target))
            {
                Directory.Delete(target, recursive: true);
            }
        }
    }
}
