namespace Agentor.Application.Reliability;

public enum OutboxMessageKind
{
    Athanor,
    Conexus,
    Mcp,
    ExternalAgent,
}

public enum OutboxStatus
{
    Pending,
    Dispatching,
    Succeeded,
    Poison,
}

public sealed record OutboxMessage(
    Guid Id,
    OutboxMessageKind Kind,
    string PayloadJson,
    OutboxStatus Status,
    int AttemptCount,
    DateTimeOffset CreatedAt,
    string? LastError);
