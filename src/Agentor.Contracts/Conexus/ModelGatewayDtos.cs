namespace Agentor.Contracts.Conexus;

/// <summary>
/// Request routed through Conexus (model gateway). Profile refs are optional routing hints.
/// </summary>
public sealed record ModelCallRequestDto(
    string Prompt,
    string ModelId,
    string? PromptProfileRef = null,
    string? ModelProfileRef = null);

/// <summary>
/// Completion envelope from Conexus; echoes profile routing for provenance and telemetry.
/// </summary>
public sealed record ModelCallResultDto(
    string CompletionText,
    string ProviderName,
    string ModelId,
    int PromptTokens,
    int CompletionTokens,
    decimal EstimatedCostUnits,
    int LatencyMs,
    string? PromptProfileRef = null,
    string? ModelProfileRef = null);
