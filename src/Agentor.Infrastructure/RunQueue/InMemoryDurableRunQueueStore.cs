using Agentor.Application.Abstractions;
using Agentor.Application.RunQueue;

namespace Agentor.Infrastructure.RunQueue;

public sealed class InMemoryDurableRunQueueStore : IDurableRunQueue
{
    private sealed class MutableQueueItem
    {
        public required RunQueueRecord Value { get; set; }
    }

    private readonly object _gate = new();
    private readonly Dictionary<Guid, MutableQueueItem> _items = new();

    public Task EnqueueAsync(RunWorkItem item, DateTimeOffset now, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _items[item.WorkItemId] = new MutableQueueItem
            {
                Value = new RunQueueRecord(
                    item.WorkItemId,
                    item.Command,
                    DurableRunQueueStatus.Pending,
                    now,
                    null,
                    null,
                    null,
                    null),
            };
        }

        return Task.CompletedTask;
    }

    public Task<RunQueueRecord?> GetAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(_items.TryGetValue(workItemId, out var item) ? item.Value : null);
        }
    }

    public Task<RunQueueRecord?> TryClaimAsync(
        Guid workItemId,
        string workerId,
        TimeSpan leaseTtl,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (!_items.TryGetValue(workItemId, out var item))
            {
                return Task.FromResult<RunQueueRecord?>(null);
            }

            if (!CanClaim(item.Value, now))
            {
                return Task.FromResult<RunQueueRecord?>(null);
            }

            item.Value = item.Value with
            {
                Status = DurableRunQueueStatus.Claimed,
                ClaimedBy = workerId,
                LeaseExpiresAtUtc = now.Add(leaseTtl),
            };
            return Task.FromResult<RunQueueRecord?>(item.Value);
        }
    }

    public Task<RunQueueRecord?> TryClaimNextAsync(
        string workerId,
        TimeSpan leaseTtl,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var candidate = _items.Values
                .Select(i => i.Value)
                .Where(r => CanClaim(r, now))
                .OrderBy(r => r.EnqueuedAtUtc)
                .FirstOrDefault();

            if (candidate is null)
            {
                return Task.FromResult<RunQueueRecord?>(null);
            }

            var item = _items[candidate.WorkItemId];
            item.Value = item.Value with
            {
                Status = DurableRunQueueStatus.Claimed,
                ClaimedBy = workerId,
                LeaseExpiresAtUtc = now.Add(leaseTtl),
            };
            return Task.FromResult<RunQueueRecord?>(item.Value);
        }
    }

    public Task ReleaseClaimAsync(Guid workItemId, string workerId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (!_items.TryGetValue(workItemId, out var item))
            {
                return Task.CompletedTask;
            }

            if (item.Value.Status == DurableRunQueueStatus.Claimed
                && string.Equals(item.Value.ClaimedBy, workerId, StringComparison.Ordinal))
            {
                item.Value = item.Value with
                {
                    Status = DurableRunQueueStatus.Pending,
                    ClaimedBy = null,
                    LeaseExpiresAtUtc = null,
                };
            }
        }

        return Task.CompletedTask;
    }

    public Task MarkCompletedAsync(Guid workItemId, Guid agentRunId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (!_items.TryGetValue(workItemId, out var item))
            {
                return Task.CompletedTask;
            }

            item.Value = item.Value with
            {
                Status = DurableRunQueueStatus.Completed,
                AgentRunId = agentRunId,
                Error = null,
                ClaimedBy = null,
                LeaseExpiresAtUtc = null,
            };
        }

        return Task.CompletedTask;
    }

    public Task MarkFailedAsync(Guid workItemId, string error, DateTimeOffset now, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (!_items.TryGetValue(workItemId, out var item))
            {
                return Task.CompletedTask;
            }

            item.Value = item.Value with
            {
                Status = DurableRunQueueStatus.Failed,
                Error = error,
                ClaimedBy = null,
                LeaseExpiresAtUtc = null,
            };
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RunQueueRecord>> ListLatestAsync(int take, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var list = _items.Values
                .Select(x => x.Value)
                .OrderByDescending(x => x.EnqueuedAtUtc)
                .Take(Math.Clamp(take, 1, 500))
                .ToList();
            return Task.FromResult<IReadOnlyList<RunQueueRecord>>(list);
        }
    }

    private static bool CanClaim(RunQueueRecord row, DateTimeOffset now) =>
        row.Status == DurableRunQueueStatus.Pending
        || (row.Status == DurableRunQueueStatus.Claimed && row.LeaseExpiresAtUtc <= now);
}
