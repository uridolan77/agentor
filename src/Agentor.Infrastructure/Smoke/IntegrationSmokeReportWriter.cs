using System.Text;
using System.Text.Json;

namespace Agentor.Infrastructure.Smoke;

public static class IntegrationSmokeReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static async Task WriteAsync(string outputDirectory, IntegrationSmokeReport report, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);
        var jsonPath = Path.Combine(outputDirectory, "integration-smoke-report.json");
        await File.WriteAllTextAsync(
                jsonPath,
                JsonSerializer.Serialize(report, JsonOptions),
                cancellationToken)
            .ConfigureAwait(false);

        var mdPath = Path.Combine(outputDirectory, "integration-smoke-report.md");
        await File.WriteAllTextAsync(mdPath, BuildMarkdown(report), cancellationToken).ConfigureAwait(false);
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
