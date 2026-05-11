using Agentor.Api.Mapping;
using Agentor.Api.Security;
using Agentor.Application.Abstractions;
using Agentor.Application.Commands;
using Agentor.Application.HumanReview;
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
                    new ApplyHumanReviewDecisionCommand(runId, request.Kind, request.Note, request.RelatedPriorActorId),
                    cancellationToken);

                if (run is null)
                {
                    return Results.NotFound(new ApiErrorDto("RunNotFound", $"Agent run '{runId}' was not found.", traceId));
                }

                return Results.Ok(run.ToDto());
            }
            catch (GovernanceApproverRequiredException ex)
            {
                return Results.Json(
                    new ApiErrorDto("GovernanceApproverRequired", ex.Message, traceId, [ex.Message]),
                    statusCode: StatusCodes.Status403Forbidden);
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
            string? format,
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

            var traceId = httpContext.Response.Headers["X-Agentor-Trace-Id"].ToString();
            if (!AuditExportFormatParser.TryParse(format, out var exportFormat, out var formatError))
            {
                return Results.BadRequest(new ApiErrorDto(
                    "AuditExportFormatInvalid",
                    formatError ?? "Invalid audit export format.",
                    traceId,
                    formatError is null ? null : [formatError]));
            }

            var result = await handler.HandleAsync(runId, exportFormat, cancellationToken);
            if (result is null)
            {
                return Results.NotFound(new ApiErrorDto("RunNotFound", $"Agent run '{runId}' was not found.", traceId));
            }

            httpContext.Response.Headers["X-Agentor-Audit-Content-SHA256"] = result.ContentSha256Hex;
            return Results.Text(result.ResponseBody, "application/json; charset=utf-8");
        })
            .WithName("GetRunAuditExport")
            .WithTags("Governance")
            .WithSummary("Deterministic JSON audit export with PR55 redaction boundaries. Query format=canonical|pretty|redactionReport|hashOnly; X-Agentor-Audit-Content-SHA256 always hashes canonical minified audit JSON.");

        return v1;
    }
}
