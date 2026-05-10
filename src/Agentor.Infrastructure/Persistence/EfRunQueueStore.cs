using Agentor.Application.Abstractions;
using Agentor.Application.Commands;
using Agentor.Application.RunQueue;
using Agentor.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;

namespace Agentor.Infrastructure.Persistence;

public sealed class EfRunQueueStore : IDurableRunQueue
{
    private readonly AgentorDbContext _db;

    public EfRunQueueStore(AgentorDbContext db)
    {
        _db = db;
    }

    public async Task EnqueueAsync(RunWorkItem item, DateTimeOffset now, CancellationToken cancellationToken)
    {
        _db.RunQueueItems.Add(new RunQueueItemRecord
        {
            WorkItemId = item.WorkItemId,
            AgentName = item.Command.AgentName,
            Objective = item.Command.Objective,
            TraceId = item.Command.TraceId,
            TenantId = item.Command.TenantId,
            WorkspaceId = item.Command.WorkspaceId,
            ProjectId = item.Command.ProjectId,
            KnowledgeScopeId = item.Command.KnowledgeScopeId,
            Status = DurableRunQueueStatus.Pending.ToString(),
            EnqueuedAtUtc = now,
            UpdatedAtUtc = now,
        });
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<RunQueueRecord?> GetAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        var row = await _db.RunQueueItems.AsNoTracking()
            .FirstOrDefaultAsync(r => r.WorkItemId == workItemId, cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : ToRecord(row);
    }

    public Task<RunQueueRecord?> TryClaimAsync(
        Guid workItemId,
        string workerId,
        TimeSpan leaseTtl,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        TryClaimByIdsAsync([workItemId], workerId, leaseTtl, now, cancellationToken);

    public Task<RunQueueRecord?> TryClaimNextAsync(
        string workerId,
        TimeSpan leaseTtl,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        return TryClaimNextCoreAsync(workerId, leaseTtl, now, cancellationToken);
    }

    private async Task<RunQueueRecord?> TryClaimNextCoreAsync(
        string workerId,
        TimeSpan leaseTtl,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var pending = DurableRunQueueStatus.Pending.ToString();
        // Pull candidate ids first, then atomically transition one row to Claimed.
        var candidateRows = await _db.RunQueueItems.AsNoTracking()
            .Where(r => r.Status == pending)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var candidateIds = candidateRows
            .OrderBy(r => r.EnqueuedAtUtc)
            .Select(r => r.WorkItemId)
            .ToList();

        return await TryClaimByIdsAsync(candidateIds, workerId, leaseTtl, now, cancellationToken).ConfigureAwait(false);
    }

    private async Task<RunQueueRecord?> TryClaimByIdsAsync(
        IReadOnlyList<Guid> candidateIds,
        string workerId,
        TimeSpan leaseTtl,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var pending = DurableRunQueueStatus.Pending.ToString();
        foreach (var candidateId in candidateIds)
        {
            var affected = await _db.RunQueueItems
                .Where(r => r.WorkItemId == candidateId
                    && r.Status == pending)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(r => r.Status, DurableRunQueueStatus.Claimed.ToString())
                    .SetProperty(r => r.ClaimedBy, workerId)
                    .SetProperty(r => r.LeaseExpiresAtUtc, now.Add(leaseTtl))
                    .SetProperty(r => r.UpdatedAtUtc, now), cancellationToken)
                .ConfigureAwait(false);

            if (affected == 0)
            {
                continue;
            }

            var claimed = await _db.RunQueueItems.AsNoTracking()
                .SingleAsync(r => r.WorkItemId == candidateId, cancellationToken)
                .ConfigureAwait(false);
            return ToRecord(claimed);
        }

        return null;
    }

    public async Task ReleaseClaimAsync(Guid workItemId, string workerId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        _ = await _db.RunQueueItems
            .Where(r => r.WorkItemId == workItemId
                && r.Status == DurableRunQueueStatus.Claimed.ToString()
                && r.ClaimedBy == workerId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.Status, DurableRunQueueStatus.Pending.ToString())
                .SetProperty(r => r.ClaimedBy, (string?)null)
                .SetProperty(r => r.LeaseExpiresAtUtc, (DateTimeOffset?)null)
                .SetProperty(r => r.UpdatedAtUtc, now), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task MarkCompletedAsync(Guid workItemId, Guid agentRunId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        _ = await _db.RunQueueItems
            .Where(r => r.WorkItemId == workItemId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.Status, DurableRunQueueStatus.Completed.ToString())
                .SetProperty(r => r.AgentRunId, agentRunId)
                .SetProperty(r => r.Error, (string?)null)
                .SetProperty(r => r.ClaimedBy, (string?)null)
                .SetProperty(r => r.LeaseExpiresAtUtc, (DateTimeOffset?)null)
                .SetProperty(r => r.UpdatedAtUtc, now), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task MarkFailedAsync(Guid workItemId, string error, DateTimeOffset now, CancellationToken cancellationToken)
    {
        _ = await _db.RunQueueItems
            .Where(r => r.WorkItemId == workItemId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.Status, DurableRunQueueStatus.Failed.ToString())
                .SetProperty(r => r.Error, error)
                .SetProperty(r => r.ClaimedBy, (string?)null)
                .SetProperty(r => r.LeaseExpiresAtUtc, (DateTimeOffset?)null)
                .SetProperty(r => r.UpdatedAtUtc, now), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<RunQueueRecord>> ListLatestAsync(int take, CancellationToken cancellationToken)
    {
        var rows = await _db.RunQueueItems.AsNoTracking()
            .OrderByDescending(r => r.EnqueuedAtUtc)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(ToRecord).ToList();
    }

    private static RunQueueRecord ToRecord(RunQueueItemRecord row) =>
        new(
            row.WorkItemId,
            new StartAgentRunCommand(
                row.AgentName,
                row.Objective,
                row.TraceId,
                row.TenantId,
                row.WorkspaceId,
                row.ProjectId,
                row.KnowledgeScopeId),
            Enum.Parse<DurableRunQueueStatus>(row.Status),
            row.EnqueuedAtUtc,
            row.ClaimedBy,
            row.LeaseExpiresAtUtc,
            row.AgentRunId,
            row.Error);
}
