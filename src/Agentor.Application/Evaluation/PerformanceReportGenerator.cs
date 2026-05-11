using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Agentor.Application.Evaluation;

/// <summary>
/// Phase 39 — deterministic performance baseline artifacts (parallel to coordination evaluation reports).
/// </summary>
public sealed record PerformanceMetricRow(
    string Scenario,
    double MeanMs,
    double MedianMs,
    int Iterations,
    string? Notes = null);

public static class PerformanceReportGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly UTF8Encoding Utf8NoBom = new(false);

    public static void WriteCiArtifactFolder(string directory, IReadOnlyList<PerformanceMetricRow> rows)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(rows);

        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "performance-report.md"), BuildMarkdown(rows), Utf8NoBom);
        File.WriteAllText(Path.Combine(directory, "performance-report.json"), BuildJson(rows), Utf8NoBom);
        File.WriteAllText(Path.Combine(directory, "performance-summary.csv"), BuildCsv(rows), Utf8NoBom);
    }

    public static string BuildMarkdown(IReadOnlyList<PerformanceMetricRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Agentor performance baseline report");
        sb.AppendLine();
        sb.AppendLine("Local or CI micro-benchmark rows; not production SLOs.");
        sb.AppendLine();
        sb.AppendLine("| Scenario | Mean (ms) | Median (ms) | Iterations | Notes |");
        sb.AppendLine("|---|---:|---:|---:|---|");
        foreach (var r in rows.OrderBy(x => x.Scenario, StringComparer.Ordinal))
        {
            sb.Append('|')
                .Append(EscapeMd(r.Scenario)).Append('|')
                .Append(FormatDouble(r.MeanMs)).Append('|')
                .Append(FormatDouble(r.MedianMs)).Append('|')
                .Append(r.Iterations).Append('|')
                .Append(EscapeMd(r.Notes ?? ""))
                .AppendLine("|");
        }

        return sb.ToString();
    }

    public static string BuildJson(IReadOnlyList<PerformanceMetricRow> rows)
    {
        var ordered = rows
            .OrderBy(x => x.Scenario, StringComparer.Ordinal)
            .Select(r => new
            {
                r.Scenario,
                r.MeanMs,
                r.MedianMs,
                r.Iterations,
                r.Notes
            })
            .ToList();

        return JsonSerializer.Serialize(
            new { generatedAtUtc = "1970-01-01T00:00:00Z", rows = ordered },
            JsonOptions);
    }

    public static string BuildCsv(IReadOnlyList<PerformanceMetricRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("scenario,meanMs,medianMs,iterations,notes");
        foreach (var r in rows.OrderBy(x => x.Scenario, StringComparer.Ordinal))
        {
            sb.Append(Csv(r.Scenario)).Append(',')
                .Append(FormatDouble(r.MeanMs)).Append(',')
                .Append(FormatDouble(r.MedianMs)).Append(',')
                .Append(r.Iterations).Append(',')
                .Append(Csv(r.Notes ?? ""))
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
}
