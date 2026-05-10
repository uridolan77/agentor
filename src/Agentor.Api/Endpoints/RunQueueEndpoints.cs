using Agentor.Api.Mapping;
using Agentor.Application.Abstractions;
using Agentor.Application.Commands;
using Agentor.Application.RunQueue;
using Agentor.Application.Validation;
using Agentor.Contracts;
using Microsoft.AspNetCore.Http;

namespace Agentor.Api.Endpoints;

public static class RunQueueEndpoints
{
    public static RouteGroupBuilder MapRunQueueEndpoints(this RouteGroupBuilder v1)
    {
        v1.MapPost("/agent-runs/queued", async (
            StartAgentRunRequestDto request,
            IRunQueue runQueue,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var requestTraceId = httpContext.Response.Headers["X-Agentor-Trace-Id"].ToString();

            var commandTraceId = string.IsNullOrWhiteSpace(request.TraceId)
                ? requestTraceId
                : request.TraceId;

            var command = StartAgentRunRequestMapping.ToCommand(request, commandTraceId);
            var validation = StartAgentRunValidator.Validate(command);

            if (!validation.IsValid)
            {
                return Results.BadRequest(new ApiErrorDto(
                    "ValidationError",
                    "One or more validation errors occurred.",
                    requestTraceId,
                    validation.Errors));
            }

            var workItemId = Guid.NewGuid();
            var workItem = new RunWorkItem(workItemId, command);
            await runQueue.EnqueueAsync(workItem, cancellationToken);

            var statusPath = $"/api/v1/agent-runs/queued/{workItemId:D}";
            return Results.Accepted(
                statusPath,
                new EnqueueAgentRunQueuedResponseDto(workItemId, statusPath));
        })
        .WithName("EnqueueAgentRunQueued")
        .WithTags("AgentRuns")
        .WithSummary("Accepts run work for background execution when RunQueue:ExecutionMode is InMemoryBackground; runs inline when mode is Inline (tests).");

        v1.MapGet("/agent-runs/queued/{workItemId:guid}", async (
            Guid workItemId,
            IRunQueue runQueue,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var snap = await runQueue.GetSnapshotAsync(workItemId, cancellationToken);
            if (snap is null)
            {
                var requestTraceId = httpContext.Response.Headers["X-Agentor-Trace-Id"].ToString();
                return Results.NotFound(new ApiErrorDto(
                    "QueuedWorkNotFound",
                    $"Queued work item '{workItemId}' was not found.",
                    requestTraceId));
            }

            return Results.Ok(new QueuedAgentRunStatusResponseDto(
                snap.Status.ToString(),
                snap.AgentRunId,
                snap.Error));
        })
        .WithName("GetQueuedAgentRunStatus")
        .WithTags("AgentRuns")
        .WithSummary("Polls status for work enqueued via POST /agent-runs/queued.");

        return v1;
    }
}