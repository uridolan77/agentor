using Agentor.Application.RunQueue;

namespace Agentor.Application.Abstractions;

public enum RunQueuedWorkStatus
{
    Pending,
    Running,
    Completed,
    Failed,
}

public sealed record RunQueuedWorkSnapshot(
    RunQueuedWorkStatus Status,
    Guid? AgentRunId,
    string? Error);

public interface IRunQueue
{
    Task EnqueueAsync(RunWorkItem item, CancellationToken cancellationToken);

    Task<RunQueuedWorkSnapshot?> GetSnapshotAsync(Guid workItemId, CancellationToken cancellationToken);
}
