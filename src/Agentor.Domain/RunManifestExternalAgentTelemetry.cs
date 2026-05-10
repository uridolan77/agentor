namespace Agentor.Domain;

public sealed record RunManifestExternalAgentTelemetry(int ExternalAgentInvocationCompletedCount)
{
    public static RunManifestExternalAgentTelemetry Empty { get; } = new(0);
}
