using System.Text;
using System.Text.Json;
using Agentor.Infrastructure.Http;

namespace Agentor.Infrastructure.Smoke;

public static class IntegrationSmokeReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static async Task WriteAsync(string outputDirectory, IntegrationSmokeReport report, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);
        var safe = SanitizeForPersist(report);
        var jsonPath = Path.Combine(outputDirectory, "integration-smoke-report.json");
        await File.WriteAllTextAsync(
                jsonPath,
                JsonSerializer.Serialize(safe, JsonOptions),
                cancellationToken)
            .ConfigureAwait(false);

        var mdPath = Path.Combine(outputDirectory, "integration-smoke-report.md");
        await File.WriteAllTextAsync(mdPath, BuildMarkdown(safe), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Defense in depth: redact free-text <see cref="SmokeStepRecord.Detail"/> before writing reports
    /// (even if callers forgot to redact upstream).
    /// </summary>
    public static IntegrationSmokeReport SanitizeForPersist(IntegrationSmokeReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return new IntegrationSmokeReport
        {
            GeneratedAtUtc = report.GeneratedAtUtc,
            OverallOk = report.OverallOk,
            Steps = report.Steps.Select(static s => new SmokeStepRecord
            {
                Target = s.Target,
                Name = s.Name,
                Ok = s.Ok,
                HttpStatus = s.HttpStatus,
                Detail = string.IsNullOrEmpty(s.Detail)
                    ? s.Detail
                    : IntegrationFailureRedaction.RedactAndTruncate(s.Detail),
            }).ToList(),
        };
    }

    private static string BuildMarkdown(IntegrationSmokeReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Integration smoke report");
        sb.AppendLine();
        sb.AppendLine($"- **UTC**: {report.GeneratedAtUtc:O}");
        sb.AppendLine($"- **Overall**: {(report.OverallOk ? "OK" : "FAILED")}");
        sb.AppendLine();
        sb.AppendLine("| Target | Step | OK | HTTP | Detail |");
        sb.AppendLine("| --- | --- | --- | --- | --- |");
        foreach (var s in report.Steps)
        {
            sb.AppendLine(
                $"| {EscapeMd(s.Target)} | {EscapeMd(s.Name)} | {s.Ok} | {s.HttpStatus?.ToString() ?? string.Empty} | {EscapeMd(s.Detail ?? string.Empty)} |");
        }

        return sb.ToString();
    }

    private static string EscapeMd(string s) => s.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("|", "\\|", StringComparison.Ordinal);
}
