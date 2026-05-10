using Agentor.Application.Reliability;

namespace Agentor.Application.Abstractions;

public interface IOutboxStore
{
    Task AppendAsync(OutboxMessage message, CancellationToken cancellationToken);

    Task<IReadOnlyList<OutboxMessage>> ListPendingForDispatchAsync(int take, CancellationToken cancellationToken);

    Task<IReadOnlyList<OutboxMessage>> ListLatestAsync(int take, CancellationToken cancellationToken);

    /// <summary>Idempotent transition Pending → Dispatching for dispatch ownership.</summary>
    Task<bool> TryMarkDispatchingAsync(Guid id, CancellationToken cancellationToken);

    Task MarkOutcomeAsync(Guid id, OutboxStatus status, string? detail, CancellationToken cancellationToken);

    Task IncrementAttemptAndRequeueOrPoisonAsync(Guid id, string error, int maxAttempts, CancellationToken cancellationToken);
}
