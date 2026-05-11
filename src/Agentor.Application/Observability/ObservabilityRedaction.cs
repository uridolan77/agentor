using System.Text.RegularExpressions;

namespace Agentor.Application.Observability;

/// <summary>
/// Redacts obvious secret-bearing fragments and truncates strings safe for structured logs and metrics-adjacent text.
/// </summary>
public static partial class ObservabilityRedaction
{
    public const int DefaultMaxLogChars = 512;

    public static string SanitizeForLog(string? text, int maxChars = DefaultMaxLogChars)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var s = text;
        s = BearerValue().Replace(s, "Bearer [REDACTED]");
        s = QuotedSecretJson().Replace(s, m => $"\"{m.Groups["k"].Value}\":\"[REDACTED]\"");
        s = KeyEqualsValue().Replace(s, m => $"{m.Groups["k"].Value}=[REDACTED]");

        return s.Length <= maxChars ? s : s[..maxChars] + "…";
    }

    public static string SanitizeExceptionMessage(Exception ex) =>
        SanitizeForLog($"{ex.GetType().Name}: {ex.Message}");

    [GeneratedRegex(@"(?i)Bearer\s+[^\s\r\n""']+", RegexOptions.Compiled)]
    private static partial Regex BearerValue();

    [GeneratedRegex(
        @"(?i)""(?<k>token|apiKey|api_key|password|secret|authorization|bearer)""\s*:\s*""[^""]*""",
        RegexOptions.Compiled)]
    private static partial Regex QuotedSecretJson();

    [GeneratedRegex(
        @"(?i)(?<k>\b(?:token|apikey|api_key|password|secret|authorization)\b)\s*[=:]\s*[^\s&;,\r\n]+",
        RegexOptions.Compiled)]
    private static partial Regex KeyEqualsValue();
}
