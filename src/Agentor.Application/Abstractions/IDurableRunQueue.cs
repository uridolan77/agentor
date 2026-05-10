using Agentor.Application.RunQueue;

namespace Agentor.Application.Abstractions;

public interface IDurableRunQueue
{
    Task EnqueueAsync(RunWorkItem item, DateTimeOffset now, CancellationToken cancellationToken);

    Task<RunQueueRecord?> GetAsync(Guid workItemId, CancellationToken cancellationToken);

    Task<RunQueueRecord?> TryClaimAsync(
        Guid workItemId,
        string workerId,
        TimeSpan leaseTtl,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<RunQueueRecord?> TryClaimNextAsync(
        string workerId,
        TimeSpan leaseTtl,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task ReleaseClaimAsync(Guid workItemId, string workerId, DateTimeOffset now, CancellationToken cancellationToken);

    Task MarkCompletedAsync(Guid workItemId, Guid agentRunId, DateTimeOffset now, CancellationToken cancellationToken);

    Task MarkFailedAsync(Guid workItemId, string error, DateTimeOffset now, CancellationToken cancellationToken);

    Task<IReadOnlyList<RunQueueRecord>> ListLatestAsync(int take, CancellationToken cancellationToken);
}
