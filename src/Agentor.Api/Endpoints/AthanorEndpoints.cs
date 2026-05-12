using Agentor.Api.Security;
using Agentor.Application.Athanor;
using Agentor.Application.Commands;
using Agentor.Application.Abstractions;
using Agentor.Application.Queries;
using Agentor.Contracts;
using Microsoft.AspNetCore.Http;
using Ontogony.Contracts.Events;

namespace Agentor.Api.Endpoints;

public static class AthanorEndpoints
{
    public static RouteGroupBuilder MapAthanorEndpoints(this RouteGroupBuilder v1)
    {
        v1.MapGet("/agent-runs/{runId:guid}/athanor/latest-snapshot", async (
            Guid runId,
            GetLatestAthanorSnapshotForRunQueryHandler handler,
            ICurrentActorAccessor actorAccessor,
            IAuthorizationDecisionService authorization,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var authResult = EndpointAuthorization.Require(
                httpContext,
                actorAccessor,
                authorization,
                AgentorPermission.RunRead);
            if (authResult is not null)
            {
                return authResult;
            }

            var outcome = await handler.HandleAsync(runId, cancellationToken);
            if (!outcome.RunExists)
            {
                var traceId = httpContext.Response.Headers[OntogonyEventHeaders.TraceId].ToString();
                return Results.NotFound(new ApiErrorDto("RunNotFound", $"Agent run '{runId}' was not found.", traceId));
            }

            return Results.Ok(outcome.Snapshot);
        })
            .WithName("GetAthanorLatestSnapshotForRun")
            .WithTags("Athanor")
            .WithSummary("Returns the latest Athanor canonical snapshot for the run's project (read-only).");

        v1.MapGet("/agent-runs/{runId:guid}/athanor/canonical", async (
            Guid runId,
            string key,
            LookupAthanorCanonicalForRunQueryHandler handler,
            ICurrentActorAccessor actorAccessor,
            IAuthorizationDecisionService authorization,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var authResult = EndpointAuthorization.Require(
                httpContext,
                actorAccessor,
                authorization,
                AgentorPermission.RunRead);
            if (authResult is not null)
            {
                return authResult;
            }

            AthanorCanonicalLookupResult outcome;
            try
            {
                outcome = await handler.HandleAsync(runId, key, cancellationToken);
            }
            catch (ArgumentException ex)
            {
                var traceId = httpContext.Response.Headers[OntogonyEventHeaders.TraceId].ToString();
                return Results.BadRequest(new ApiErrorDto("ValidationError", ex.Message, traceId, [ex.Message]));
            }

            if (!outcome.RunExists)
            {
                var traceId = httpContext.Response.Headers[OntogonyEventHeaders.TraceId].ToString();
                return Results.NotFound(new ApiErrorDto("RunNotFound", $"Agent run '{runId}' was not found.", traceId));
            }

            if (outcome.Entry is null)
            {
                var traceId = httpContext.Response.Headers[OntogonyEventHeaders.TraceId].ToString();
                return Results.NotFound(new ApiErrorDto("CanonicalEntryNotFound", "No canonical entry matched the key for this project.", traceId));
            }

            return Results.Ok(outcome.Entry);
        })
            .WithName("LookupAthanorCanonicalForRun")
            .WithTags("Athanor")
            .WithSummary("Looks up a canonical state entry by key query parameter for the run's project (read-only).");

        v1.MapPost("/agent-runs/{runId:guid}/athanor/evidence-provenance", async (
            Guid runId,
            AttachEvidenceProvenanceRequestDto request,
            AttachAthanorEvidenceProvenanceHandler handler,
            ICurrentActorAccessor actorAccessor,
            IAuthorizationDecisionService authorization,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var authResult = EndpointAuthorization.Require(
                httpContext,
                actorAccessor,
                authorization,
                AgentorPermission.RunWrite);
            if (authResult is not null)
            {
                return authResult;
            }

            if (string.IsNullOrWhiteSpace(request.Query))
            {
                var traceId = httpContext.Response.Headers[OntogonyEventHeaders.TraceId].ToString();
                return Results.BadRequest(new ApiErrorDto("ValidationError", "Query is required.", traceId, ["Query is required."]));
            }

            var outcome = await handler.HandleAsync(runId, request.Query, cancellationToken);
            if (outcome is null)
            {
                var traceId = httpContext.Response.Headers[OntogonyEventHeaders.TraceId].ToString();
                return Results.NotFound(new ApiErrorDto("RunNotFound", $"Agent run '{runId}' was not found.", traceId));
            }

            if (outcome == false)
            {
                var traceId = httpContext.Response.Headers[OntogonyEventHeaders.TraceId].ToString();
                return Results.Conflict(new ApiErrorDto("RunNotRunning", "Evidence provenance can only be attached while the run is Running.", traceId));
            }

            return Results.NoContent();
        })
            .WithName("AttachAthanorEvidenceProvenance")
            .WithTags("Athanor")
            .WithSummary("Runs an Athanor evidence search and records result identifiers on the run trace as provenance (non-canon).");

        v1.MapPost("/agent-runs/{runId:guid}/athanor/candidates", async (
            Guid runId,
            SubmitAthanorCandidateRequestDto request,
            SubmitAthanorCandidateHandler handler,
            ICurrentActorAccessor actorAccessor,
            IAuthorizationDecisionService authorization,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var authResult = EndpointAuthorization.Require(
                httpContext,
                actorAccessor,
                authorization,
                AgentorPermission.RunWrite);
            if (authResult is not null)
            {
                return authResult;
            }

            if (string.IsNullOrWhiteSpace(request.Summary))
            {
                var traceId = httpContext.Response.Headers[OntogonyEventHeaders.TraceId].ToString();
                return Results.BadRequest(new ApiErrorDto("ValidationError", "Summary is required.", traceId, ["Summary is required."]));
            }

            var outcome = await handler.HandleAsync(runId, request.Summary, request.PayloadJson ?? "{}", cancellationToken);
            if (outcome.Ok is null)
            {
                var traceId = httpContext.Response.Headers[OntogonyEventHeaders.TraceId].ToString();
                return Results.NotFound(new ApiErrorDto("RunNotFound", $"Agent run '{runId}' was not found.", traceId));
            }

            if (outcome.Ok == false)
            {
                var traceId = httpContext.Response.Headers[OntogonyEventHeaders.TraceId].ToString();
                return Results.Conflict(new ApiErrorDto("RunNotRunning", "Candidates can only be submitted while the run is Running.", traceId));
            }

            return Results.Accepted($"/api/v1/agent-runs/{runId}", new { candidateId = outcome.CandidateId });
        })
            .WithName("SubmitAthanorCandidate")
            .WithTags("Athanor")
            .WithSummary("Submits a non-canon candidate payload to Athanor and records the submission on the run trace.");

        v1.MapPost("/agent-runs/{runId:guid}/athanor/review-queue", async (
            Guid runId,
            QueueAthanorReviewRequestDto request,
            QueueAthanorReviewHandler handler,
            ICurrentActorAccessor actorAccessor,
            IAuthorizationDecisionService authorization,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var authResult = EndpointAuthorization.Require(
                httpContext,
                actorAccessor,
                authorization,
                AgentorPermission.RunWrite);
            if (authResult is not null)
            {
                return authResult;
            }

            if (request.CandidateId == Guid.Empty)
            {
                var traceId = httpContext.Response.Headers[OntogonyEventHeaders.TraceId].ToString();
                return Results.BadRequest(new ApiErrorDto("ValidationError", "CandidateId is required.", traceId, null));
            }

            var actorId = request.ActorId is { } aid && aid != Guid.Empty
                ? aid
                : actorAccessor.Current.ActorId;

            if (actorId == Guid.Empty)
            {
                var traceId = httpContext.Response.Headers[OntogonyEventHeaders.TraceId].ToString();
                return Results.BadRequest(new ApiErrorDto("ValidationError", "ActorId is required (body or X-Agentor-Actor-Id).", traceId, null));
            }

            var outcome = await handler.HandleAsync(runId, request.CandidateId, actorId, cancellationToken);
            if (outcome is null)
            {
                var traceId = httpContext.Response.Headers[OntogonyEventHeaders.TraceId].ToString();
                return Results.NotFound(new ApiErrorDto("RunNotFound", $"Agent run '{runId}' was not found.", traceId));
            }

            if (outcome == false)
            {
                var traceId = httpContext.Response.Headers[OntogonyEventHeaders.TraceId].ToString();
                return Results.Conflict(new ApiErrorDto("RunNotRunning", "Review queue operations require a Running run.", traceId));
            }

            return Results.NoContent();
        })
            .WithName("QueueAthanorReview")
            .WithTags("Athanor")
            .WithSummary("Queues a candidate for Athanor human review and records the queue item on the run trace (non-canon). ActorId may be omitted when X-Agentor-Actor-Id is present.");

        return v1;
    }
}
