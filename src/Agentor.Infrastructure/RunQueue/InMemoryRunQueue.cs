using Agentor.Application.Abstractions;
using Agentor.Application.Commands;
using Agentor.Application.Options;
using Agentor.Application.RunQueue;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Agentor.Infrastructure.RunQueue;

/// <summary>
/// In-process queue only: not durable across restarts and not broker-backed (PR60.6).
/// </summary>
public sealed class InMemoryRunQueue : IRunQueue
{
    private const string InlineWorkerId = "run-queue:inline";

    private readonly IDurableRunQueue _store;
    private readonly IClock _clock;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<RunQueueOptions> _options;

    public InMemoryRunQueue(
        IDurableRunQueue store,
        IClock clock,
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<RunQueueOptions> options)
    {
        _store = store;
        _clock = clock;
        _scopeFactory = scopeFactory;
        _options = options;
    }

    public Task<RunQueuedWorkSnapshot?> GetSnapshotAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        return GetSnapshotCoreAsync(workItemId, cancellationToken);
    }

    private async Task<RunQueuedWorkSnapshot?> GetSnapshotCoreAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        var row = await _store.GetAsync(workItemId, cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            return null;
        }

        return new RunQueuedWorkSnapshot(
            row.Status switch
            {
                DurableRunQueueStatus.Pending => RunQueuedWorkStatus.Pending,
                DurableRunQueueStatus.Claimed => RunQueuedWorkStatus.Running,
                DurableRunQueueStatus.Completed => RunQueuedWorkStatus.Completed,
                DurableRunQueueStatus.Failed => RunQueuedWorkStatus.Failed,
                _ => RunQueuedWorkStatus.Pending,
            },
            row.AgentRunId,
            row.Error);
    }

    public async Task EnqueueAsync(RunWorkItem item, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        await _store.EnqueueAsync(item, now, cancellationToken).ConfigureAwait(false);

        if (_options.CurrentValue.ExecutionMode == RunQueueExecutionMode.Inline)
        {
            var claimed = await _store
                .TryClaimAsync(item.WorkItemId, InlineWorkerId, TimeSpan.FromSeconds(30), now, cancellationToken)
                .ConfigureAwait(false);
            if (claimed is not null)
            {
                await ProcessOneAsync(claimed, cancellationToken).ConfigureAwait(false);
            }
            return;
        }
    }

    public async Task ProcessOneAsync(RunQueueRecord item, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<StartAgentRunHandler>();
            var run = await handler.HandleAsync(item.Command, cancellationToken).ConfigureAwait(false);
            await _store.MarkCompletedAsync(item.WorkItemId, run.Id, _clock.UtcNow, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await _store.MarkFailedAsync(item.WorkItemId, ex.Message, _clock.UtcNow, cancellationToken).ConfigureAwait(false);
        }
    }
}
