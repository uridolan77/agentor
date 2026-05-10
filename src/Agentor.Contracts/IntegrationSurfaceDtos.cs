namespace Agentor.Contracts;

public sealed record IntegrationAdapterStatusDto(string Mode, bool Ready, string? Detail);

public sealed record TransportResilienceClientDto(
    bool CircuitOpen,
    int ConsecutiveFailures,
    DateTimeOffset? CircuitOpenUntilUtc);

public sealed record IntegrationsStatusResponseDto(
    bool Ready,
    IReadOnlyDictionary<string, IntegrationAdapterStatusDto> Integrations,
    IReadOnlyDictionary<string, TransportResilienceClientDto>? HttpTransportResilience = null);

public sealed record EnqueueAgentRunQueuedResponseDto(Guid WorkItemId, string StatusPath);

public sealed record QueuedAgentRunStatusResponseDto(string Status, Guid? AgentRunId, string? Error);

public sealed record OpsQueueItemDto(
    Guid WorkItemId,
    string Status,
    DateTimeOffset EnqueuedAtUtc,
    Guid? AgentRunId,
    string? ClaimedBy,
    DateTimeOffset? LeaseExpiresAtUtc,
    string? Error);

public sealed record OpsOutboxItemDto(
    Guid Id,
    string Kind,
    string Status,
    int AttemptCount,
    DateTimeOffset CreatedAtUtc,
    string? LastError);

public sealed record OpsLeaseItemDto(
    Guid ResourceId,
    string LeaseHolder,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset CreatedAtUtc);
