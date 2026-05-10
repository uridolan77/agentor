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
        var claimedStatus = DurableRunQueueStatus.Claimed.ToString();

        // Pull candidate ids first, then atomically transition one row to Claimed.
        var candidateRows = await _db.RunQueueItems.AsNoTracking()
            .Where(r => r.Status == pending || r.Status == claimedStatus)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var candidateIds = candidateRows
            .Where(r => r.Status == pending || (r.LeaseExpiresAtUtc.HasValue && r.LeaseExpiresAtUtc <= now))
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
        var pendingStatus = DurableRunQueueStatus.Pending.ToString();
        var claimedStatus = DurableRunQueueStatus.Claimed.ToString();
        var leaseExpiresAtUtc = now.Add(leaseTtl);

        foreach (var candidateId in candidateIds)
        {
            var row = await _db.RunQueueItems
                .FirstOrDefaultAsync(r => r.WorkItemId == candidateId, cancellationToken)
                .ConfigureAwait(false);

            if (row is null)
            {
                continue;
            }

            var canClaim = row.Status == pendingStatus
                || (row.Status == claimedStatus
                    && row.LeaseExpiresAtUtc is not null
                    && row.LeaseExpiresAtUtc <= now);

            if (!canClaim)
            {
                continue;
            }

            row.Status = claimedStatus;
            row.ClaimedBy = workerId;
            row.LeaseExpiresAtUtc = leaseExpiresAtUtc;
            row.UpdatedAtUtc = now;

            _ = await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return ToRecord(row);
        }

        return null;
    }

    public async Task ReleaseClaimAsync(Guid workItemId, string workerId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var claimedStatus = DurableRunQueueStatus.Claimed.ToString();
        var pendingStatus = DurableRunQueueStatus.Pending.ToString();
        _ = await _db.RunQueueItems
            .Where(r => r.WorkItemId == workItemId
                && r.Status == claimedStatus
                && r.ClaimedBy == workerId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.Status, pendingStatus)
                .SetProperty(r => r.ClaimedBy, (string?)null)
                .SetProperty(r => r.LeaseExpiresAtUtc, (DateTimeOffset?)null)
                .SetProperty(r => r.UpdatedAtUtc, now), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task MarkCompletedAsync(
        Guid workItemId,
        Guid agentRunId,
        string workerId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var claimedStatus = DurableRunQueueStatus.Claimed.ToString();
        var completedStatus = DurableRunQueueStatus.Completed.ToString();
        _ = await _db.RunQueueItems
            .Where(r => r.WorkItemId == workItemId
                && r.Status == claimedStatus
                && r.ClaimedBy == workerId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.Status, completedStatus)
                .SetProperty(r => r.AgentRunId, agentRunId)
                .SetProperty(r => r.Error, (string?)null)
                .SetProperty(r => r.ClaimedBy, (string?)null)
                .SetProperty(r => r.LeaseExpiresAtUtc, (DateTimeOffset?)null)
                .SetProperty(r => r.UpdatedAtUtc, now), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task MarkFailedAsync(
        Guid workItemId,
        string error,
        string workerId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var claimedStatus = DurableRunQueueStatus.Claimed.ToString();
        var failedStatus = DurableRunQueueStatus.Failed.ToString();
        _ = await _db.RunQueueItems
            .Where(r => r.WorkItemId == workItemId
                && r.Status == claimedStatus
                && r.ClaimedBy == workerId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.Status, failedStatus)
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
