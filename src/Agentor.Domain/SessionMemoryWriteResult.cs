namespace Agentor.Domain;

public enum SessionMemoryWriteStatus
{
    Accepted,
    RejectedNotRunning,
    RejectedKeyInvalid,
    RejectedValueTooLarge,
    RejectedKeyTooLarge,
    RejectedKeyCount,
    RejectedTotalBudget
}

public sealed record SessionMemoryWriteResult(SessionMemoryWriteStatus Status, string? ReasonCode);
