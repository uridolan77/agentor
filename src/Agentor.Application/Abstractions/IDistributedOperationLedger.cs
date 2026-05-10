namespace Agentor.Application.Abstractions;

/// <summary>Durable once-only marker for idempotent command processing across workers.</summary>
public interface IDistributedOperationLedger
{
    /// <summary>Returns true if this caller was the first to commit the operation key.</summary>
    Task<bool> TryCommitOnceAsync(string operationKey, CancellationToken cancellationToken);
}
