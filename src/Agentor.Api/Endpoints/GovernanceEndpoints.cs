using Agentor.Api.Mapping;
using Agentor.Api.Security;
using Agentor.Application.Commands;
using Agentor.Application.Abstractions;
using Agentor.Application.Queries;
using Agentor.Contracts;
using Microsoft.AspNetCore.Http;

namespace Agentor.Api.Endpoints;

public static class GovernanceEndpoints
{
    public static RouteGroupBuilder MapGovernanceEndpoints(this RouteGroupBuilder v1)
    {
        v1.MapPost("/agent-runs/{runId:guid}/human-review", async (
            Guid runId,
            ApplyHumanReviewRequestDto request,
            ApplyHumanReviewDecisionHandler handler,
            ICurrentActorAccessor actorAccessor,
            IAuthorizationDecisionService authorization,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var authResult = EndpointAuthorization.Require(
                httpContext,
                actorAccessor,
                authorization,
                AgentorPermission.GovernanceReviewWrite);
            if (authResult is not null)
            {
                return authResult;
            }

            var traceId = httpContext.Response.Headers["X-Agentor-Trace-Id"].ToString();
            try
            {
                var run = await handler.HandleAsync(
                    new ApplyHumanReviewDecisionCommand(runId, request.Kind, request.Note),
                    cancellationToken);

                if (run is null)
                {
                    return Results.NotFound(new ApiErrorDto("RunNotFound", $"Agent run '{runId}' was not found.", traceId));
                }

                return Results.Ok(run.ToDto());
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new ApiErrorDto("HumanReviewInvalid", ex.Message, traceId, [ex.Message]));
            }
        })
            .WithName("ApplyHumanReviewDecision")
            .WithTags("Governance")
            .WithSummary("Records a human review decision. Approve resumes execution for a pending RequiresReview tool (does not canonize knowledge). Deny cannot be converted from a prior policy Deny outcome.");

        v1.MapGet("/agent-runs/{runId:guid}/audit-export", async (
            Guid runId,
            GetRunAuditExportQueryHandler handler,
            ICurrentActorAccessor actorAccessor,
            IAuthorizationDecisionService authorization,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var authResult = EndpointAuthorization.Require(
                httpContext,
                actorAccessor,
                authorization,
                AgentorPermission.AuditRead);
            if (authResult is not null)
            {
                return authResult;
            }

            var result = await handler.HandleAsync(runId, cancellationToken);
            if (result is null)
            {
                var traceId = httpContext.Response.Headers["X-Agentor-Trace-Id"].ToString();
                return Results.NotFound(new ApiErrorDto("RunNotFound", $"Agent run '{runId}' was not found.", traceId));
            }

            httpContext.Response.Headers["X-Agentor-Audit-Content-SHA256"] = result.ContentSha256Hex;
            return Results.Text(result.CanonicalJson, "application/json; charset=utf-8");
        })
            .WithName("GetRunAuditExport")
            .WithTags("Governance")
            .WithSummary("Deterministic JSON audit packet for a run with redaction boundaries (PR55).");

        return v1;
    }
}
