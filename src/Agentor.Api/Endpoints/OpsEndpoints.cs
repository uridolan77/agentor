using Agentor.Application.Abstractions;
using Agentor.Contracts;
using Microsoft.AspNetCore.Http;

namespace Agentor.Api.Endpoints;

public static class OpsEndpoints
{
    public static RouteGroupBuilder MapOpsEndpoints(this RouteGroupBuilder v1)
    {
        v1.MapGet("/ops/queue", async (
            IDurableRunQueue queue,
            int? take,
            CancellationToken cancellationToken) =>
        {
            var rows = await queue.ListLatestAsync(take ?? 50, cancellationToken).ConfigureAwait(false);
            var dto = rows.Select(r => new OpsQueueItemDto(
                r.WorkItemId,
                r.Status.ToString(),
                r.EnqueuedAtUtc,
                r.AgentRunId,
                r.ClaimedBy,
                r.LeaseExpiresAtUtc,
                r.Error));
            return Results.Ok(dto);
        })
        .WithName("GetOpsQueue")
        .WithTags("System")
        .WithSummary("Read-only operational queue status for recent queued runs.");

        v1.MapGet("/ops/outbox", async (
            IOutboxStore outbox,
            int? take,
            CancellationToken cancellationToken) =>
        {
            var rows = await outbox.ListLatestAsync(take ?? 50, cancellationToken).ConfigureAwait(false);
            var dto = rows.Select(r => new OpsOutboxItemDto(
                r.Id,
                r.Kind.ToString(),
                r.Status.ToString(),
                r.AttemptCount,
                r.CreatedAt,
                r.LastError));
            return Results.Ok(dto);
        })
        .WithName("GetOpsOutbox")
        .WithTags("System")
        .WithSummary("Read-only operational status for outbox messages.");

        v1.MapGet("/ops/leases", async (
            IRunExecutionLeaseStore leases,
            int? take,
            CancellationToken cancellationToken) =>
        {
            var rows = await leases.ListLeasesAsync(take ?? 50, cancellationToken).ConfigureAwait(false);
            var dto = rows.Select(r => new OpsLeaseItemDto(
                r.ResourceId,
                r.LeaseHolder,
                r.ExpiresAtUtc,
                r.CreatedAtUtc));
            return Results.Ok(dto);
        })
        .WithName("GetOpsLeases")
        .WithTags("System")
        .WithSummary("Read-only operational status for active execution leases.");

        return v1;
    }
}
