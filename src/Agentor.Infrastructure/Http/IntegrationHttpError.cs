using System.Net;
using System.Text.RegularExpressions;
using Agentor.Application.Observability;

namespace Agentor.Infrastructure.Http;

/// <summary>
/// Best-effort shaping of integration HTTP failures: status-code-bearing <see cref="HttpRequestException"/>,
/// truncated upstream bodies, and light redaction of obvious secret-bearing fragments.
/// </summary>
internal static partial class IntegrationHttpError
{
    public const int DefaultMaxBodyChars = 512;

    /// <summary>
    /// Redacts common secret patterns, then truncates for safe exception messages and logs.
    /// </summary>
    public static string RedactAndTruncate(string? body, int maxChars = DefaultMaxBodyChars)
    {
        if (string.IsNullOrEmpty(body))
        {
            return string.Empty;
        }

        var s = body;
        s = BearerValue().Replace(s, "Bearer [REDACTED]");
        s = QuotedSecretJson().Replace(s, m => $"\"{m.Groups["k"].Value}\":\"[REDACTED]\"");
        s = KeyEqualsValue().Replace(s, m => $"{m.Groups["k"].Value}=[REDACTED]");

        return s.Length <= maxChars ? s : s[..maxChars] + "…";
    }

    public static async Task ThrowIfUnsuccessfulAsync(
        HttpResponseMessage response,
        string integrationLabel,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var safe = RedactAndTruncate(raw);
        var corr = AgentorCorrelationContext.Current;
        var corrSuffix = string.IsNullOrEmpty(corr) ? string.Empty : $" CorrelationId={corr}";
        var message = $"{integrationLabel} HTTP {(int)response.StatusCode} {response.ReasonPhrase}. Body: {safe}{corrSuffix}";
        throw new HttpRequestException(message, inner: null, statusCode: response.StatusCode);
    }

    [GeneratedRegex(@"(?i)Bearer\s+[^\s\r\n""']+", RegexOptions.Compiled)]
    private static partial Regex BearerValue();

    /// <summary>JSON-style "apiKey":"value" (keys: token, apiKey, password, secret, authorization, bearer).</summary>
    [GeneratedRegex(
        @"(?i)""(?<k>token|apiKey|api_key|password|secret|authorization|bearer)""\s*:\s*""[^""]*""",
        RegexOptions.Compiled)]
    private static partial Regex QuotedSecretJson();

    /// <summary>token=..., apiKey:..., etc. (query or loose key-value).</summary>
    [GeneratedRegex(
        @"(?i)(?<k>\b(?:token|apikey|api_key|password|secret|authorization)\b)\s*[=:]\s*[^\s&;,\r\n]+",
        RegexOptions.Compiled)]
    private static partial Regex KeyEqualsValue();
}
