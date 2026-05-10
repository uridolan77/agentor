namespace Agentor.Contracts;

public sealed record IntegrationAdapterStatusDto(string Mode, bool Ready, string? Detail);

public sealed record IntegrationsStatusResponseDto(
    bool Ready,
    IReadOnlyDictionary<string, IntegrationAdapterStatusDto> Integrations);
