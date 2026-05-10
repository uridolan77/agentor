namespace Agentor.Infrastructure.Options;

/// <summary>
/// Typed HTTP settings for an integration family. Used when <see cref="IntegrationFamilyOptions.Mode"/> is <see cref="IntegrationAdapterMode.Http"/>.
/// </summary>
public sealed class HttpIntegrationOptions
{
    /// <summary>Absolute root URL for the remote integration (https recommended).</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Optional relative path appended for readiness probes (GET). Empty probes <see cref="BaseUrl"/>.</summary>
    public string ReadinessProbeRelativePath { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Optional default headers (names and values only; never log these).</summary>
    public Dictionary<string, string>? DefaultHeaders { get; set; }

    /// <summary>Optional logical retry policy name for operators (no Polly wiring in Agentor core).</summary>
    public string? RetryPolicyName { get; set; }
}
