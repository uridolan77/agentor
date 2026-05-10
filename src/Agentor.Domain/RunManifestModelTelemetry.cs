namespace Agentor.Domain;

/// <summary>
/// Roll-up of model-gateway call metrics attached to <see cref="RunManifest"/>.
/// Domain stores aggregates only; which tools participate is decided in Application when building this value.
/// </summary>
public sealed record RunManifestModelTelemetry(
    int ModelCallCount,
    long TotalPromptTokens,
    long TotalCompletionTokens,
    decimal TotalEstimatedCostUnits,
    long TotalLatencyMs,
    string? PrimaryProviderName,
    string? PrimaryModelId,
    string? PrimaryPromptProfileRef,
    string? PrimaryModelProfileRef)
{
    public static RunManifestModelTelemetry Empty { get; } = new(
        0,
        0L,
        0L,
        0m,
        0L,
        null,
        null,
        null,
        null);
}