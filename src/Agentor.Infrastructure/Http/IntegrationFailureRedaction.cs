namespace Agentor.Infrastructure.Http;

/// <summary>
/// Public redaction surface for operator reports (wraps <see cref="IntegrationHttpError"/>).
/// </summary>
public static class IntegrationFailureRedaction
{
    public static string RedactAndTruncate(string? body, int maxChars = IntegrationHttpError.DefaultMaxBodyChars) =>
        IntegrationHttpError.RedactAndTruncate(body, maxChars);
}
