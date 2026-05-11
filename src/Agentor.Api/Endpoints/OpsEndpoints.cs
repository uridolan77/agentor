using Agentor.Application.Abstractions;
using Agentor.Api.Configuration;
using Agentor.Api.Diagnostics;
using Agentor.Api.Security;
using Agentor.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;

namespace Agentor.Api.Endpoints;

public static class OpsEndpoints
{
    public static RouteGroupBuilder MapOpsEndpoints(this RouteGroupBuilder v1)
    {
        v1.MapGet("/ops/queue", async (
            HttpContext httpContext,
            ICurrentActorAccessor actorAccessor,
            IAuthorizationDecisionService authorization,
            IDurableRunQueue queue,
            int? take,
            CancellationToken cancellationToken) =>
        {
            var gate = EndpointAuthorization.Require(
                httpContext,
                actorAccessor,
                authorization,
                AgentorPermission.OpsRead);
            if (gate is not null)
            {
                return gate;
            }

            var rows = await queue.ListLatestAsync(take ?? 50, cancellationToken).ConfigureAwait(false);
            var dto = rows.Select(r => new OpsQueueItemDto(
                r.WorkItemId,
                r.Status.ToString(),
                r.EnqueuedAtUtc,
                r.AgentRunId,
                r.ClaimedBy,
                r.LeaseExpiresAtUtc,
                SanitizeOpsText(r.Error)));
            return Results.Ok(dto);
        })
        .WithName("GetOpsQueue")
        .WithTags("System")
        .WithSummary("Read-only operational queue status for recent queued runs.");

        v1.MapGet("/ops/outbox", async (
            HttpContext httpContext,
            ICurrentActorAccessor actorAccessor,
            IAuthorizationDecisionService authorization,
            IOutboxStore outbox,
            int? take,
            CancellationToken cancellationToken) =>
        {
            var gate = EndpointAuthorization.Require(
                httpContext,
                actorAccessor,
                authorization,
                AgentorPermission.OpsRead);
            if (gate is not null)
            {
                return gate;
            }

            var rows = await outbox.ListLatestAsync(take ?? 50, cancellationToken).ConfigureAwait(false);
            var dto = rows.Select(r => new OpsOutboxItemDto(
                r.Id,
                r.Kind.ToString(),
                r.Status.ToString(),
                r.AttemptCount,
                r.CreatedAt,
                SanitizeOpsText(r.LastError)));
            return Results.Ok(dto);
        })
        .WithName("GetOpsOutbox")
        .WithTags("System")
        .WithSummary("Read-only operational status for outbox messages.");

        v1.MapGet("/ops/leases", async (
            HttpContext httpContext,
            ICurrentActorAccessor actorAccessor,
            IAuthorizationDecisionService authorization,
            IRunExecutionLeaseStore leases,
            int? take,
            CancellationToken cancellationToken) =>
        {
            var gate = EndpointAuthorization.Require(
                httpContext,
                actorAccessor,
                authorization,
                AgentorPermission.OpsRead);
            if (gate is not null)
            {
                return gate;
            }

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

        v1.MapGet("/ops/diagnostics-report", async (
            HttpContext httpContext,
            ICurrentActorAccessor actorAccessor,
            IAuthorizationDecisionService authorization,
            IWebHostEnvironment environment,
            IConfiguration configuration,
            OperatorDiagnosticsService diagnostics,
            string? format,
            CancellationToken cancellationToken) =>
        {
            var gate = EndpointAuthorization.Require(
                httpContext,
                actorAccessor,
                authorization,
                AgentorPermission.OpsRead);
            if (gate is not null)
            {
                return gate;
            }

            var openApiDocumentEnabled = environment.IsDevelopment()
                || environment.IsEnvironment("Test")
                || environment.IsEnvironment("Testing")
                || (configuration.GetSection(AgentorOpenApiOptions.SectionName).Get<AgentorOpenApiOptions>()?.Enabled ?? false);

            var (json, markdown) = await diagnostics.BuildAsync(openApiDocumentEnabled, cancellationToken).ConfigureAwait(false);
            if (string.Equals(format, "markdown", StringComparison.OrdinalIgnoreCase))
            {
                return Results.Text(markdown, "text/markdown; charset=utf-8");
            }

            return Results.Text(json, "application/json; charset=utf-8");
        })
        .WithName("GetOpsDiagnosticsReport")
        .WithTags("System")
        .WithSummary("Redacted operator diagnostics bundle (JSON default; format=markdown for Markdown).");

        return v1;
    }

    private static string? SanitizeOpsText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        var lowered = text.ToLowerInvariant();
        if (lowered.Contains("password")
            || lowered.Contains("secret")
            || lowered.Contains("apikey")
            || lowered.Contains("authorization")
            || lowered.Contains("bearer")
            || lowered.Contains("token"))
        {
            return "[redacted]";
        }

        var normalized = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return normalized.Length <= 256
            ? normalized
            : normalized[..256] + "...";
    }
}
