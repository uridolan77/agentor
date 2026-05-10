namespace Agentor.Infrastructure.Options;

public sealed class TransportResilienceOptions
{
    public const string SectionName = "Agentor:TransportResilience";

    public bool Enabled { get; set; } = true;

    public int MaxRetries { get; set; } = 2;

    public int BaseBackoffMilliseconds { get; set; } = 10;

    public int CircuitFailureThreshold { get; set; } = 5;

    public int CircuitOpenDurationSeconds { get; set; } = 30;

    /// <summary>HTTP status codes that trigger a retry when resilience is enabled (HTTP adapters only).</summary>
    public int[] RetryableStatusCodes { get; set; } = [408, 425, 429, 500, 502, 503, 504];
}
