using System.Text.Json.Serialization;
using Agentor.Api.Mapping;
using Agentor.Api.Middleware;
using Agentor.Api.Security;
using Agentor.Application;
using Agentor.Application.Abstractions;
using Agentor.Application.Athanor;
using Agentor.Application.Commands;
using Agentor.Application.Options;
using Agentor.Application.Queries;
using Agentor.Application.Services;
using Agentor.Application.Validation;
using Agentor.Api;
using Agentor.Domain;
using Agentor.Contracts;
using Agentor.Infrastructure;
using Agentor.Infrastructure.IntegrationStatus;
using Agentor.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAgentorApplication();
builder.Services.AddAgentorInfrastructure(builder.Configuration);

// Switch to EF Core + PostgreSQL when configured.
var persistenceOpts = builder.Configuration
    .GetSection(AgentorPersistenceOptions.SectionName)
    .Get<AgentorPersistenceOptions>() ?? new AgentorPersistenceOptions();

if (persistenceOpts.Mode == AgentorPersistenceOptions.ModePostgres
    && !string.IsNullOrWhiteSpace(persistenceOpts.ConnectionString))
{
    builder.Services.AddAgentorEfCoreRepository(db =>
        db.UseNpgsql(persistenceOpts.ConnectionString));
}

builder.Services.AddOpenApi();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentActorAccessor, HeaderOrFakeActorAccessor>();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services
    .AddOptions<AgentorRuntimeOptions>()
    .BindConfiguration(AgentorRuntimeOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<AgentorPersistenceOptions>()
    .BindConfiguration(AgentorPersistenceOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.Configure<RuntimePolicyOptions>(
    builder.Configuration.GetSection(RuntimePolicyOptions.SectionName));

builder.Services.Configure<AuditExportOptions>(
    builder.Configuration.GetSection(AuditExportOptions.SectionName));

builder.Services.Configure<ToolExecutionOptions>(
    builder.Configuration.GetSection(ToolExecutionOptions.SectionName));

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestTracingMiddleware>();

app.MapOpenApi();

app.MapGet("/health", () => Results.Ok(new { status = "alive" }))
.WithName("GetHealth")
.WithTags("System")
.WithSummary("Liveness probe; does not evaluate downstream integrations.");

app.MapGet("/ready", async (
    IntegrationSurfaceService readiness,
    CancellationToken cancellationToken) =>
{
    var (ready, reason) = await readiness.GetReadinessAsync(cancellationToken);
    return ready
        ? Results.Ok(new { status = "ready" })
        : Results.Json(new { status = "not_ready", reason }, statusCode: StatusCodes.Status503ServiceUnavailable);
})
.WithName("GetReady")
.WithTags("System")
.WithSummary("Readiness probe; reports persistence and configured integration adapters.");

app.MapGet("/api/v1/integrations/status", async (
    IntegrationSurfaceService surface,
    CancellationToken cancellationToken) =>
{
    var dto = await surface.GetStatusAsync(cancellationToken);
    return Results.Ok(dto);
})
.WithName("GetIntegrationsStatus")
.WithTags("System")
.WithSummary("Integration modes and dependency readiness without exposing secrets.");

var v1 = app.MapGroup("/api/v1");

v1.MapGet("/agent-runs", async (
    int? skip,
    int? take,
    ListAgentRunsQueryHandler handler,
    CancellationToken cancellationToken) =>
{
    var s = skip ?? 0;
    var t = take ?? ListAgentRunsQueryHandler.DefaultTake;
    var page = await handler.HandleAsync(s, t, cancellationToken);
    return Results.Ok(page.ToDto());
})
.WithName("ListAgentRuns")
.WithTags("AgentRuns")
.WithSummary("Lists agent runs with stable ordering (newest first) and pagination.")
.WithOpenApi();

v1.MapPost("/agent-runs", async (
    StartAgentRunRequestDto request,
    StartAgentRunHandler handler,
    AgentRunIdempotencyService idempotencyService,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var requestTraceId = httpContext.Response.Headers["X-Agentor-Trace-Id"].ToString();

    var commandTraceId = string.IsNullOrWhiteSpace(request.TraceId)
        ? requestTraceId
        : request.TraceId;

    var command = new StartAgentRunCommand(
        request.AgentName,
        request.Objective,
        commandTraceId,
        request.TenantId,
        request.WorkspaceId,
        request.ProjectId,
        request.KnowledgeScopeId);
    var validation = StartAgentRunValidator.Validate(command);

    if (!validation.IsValid)
    {
        return Results.BadRequest(new ApiErrorDto(
            "ValidationError",
            "One or more validation errors occurred.",
            requestTraceId,
            validation.Errors));
    }

    const int maxIdempotencyKeyLength = 256;
    if (httpContext.Request.Headers.TryGetValue("Idempotency-Key", out var idempotencyHeaderValues))
    {
        var idempotencyKey = idempotencyHeaderValues.ToString().Trim();
        if (idempotencyKey.Length > maxIdempotencyKeyLength)
        {
            return Results.BadRequest(new ApiErrorDto(
                "ValidationError",
                $"Idempotency-Key must not exceed {maxIdempotencyKeyLength} characters.",
                requestTraceId,
                [$"Idempotency-Key must not exceed {maxIdempotencyKeyLength} characters."]));
        }

        if (idempotencyKey.Length > 0)
        {
            var traceIdSpecifiedInBody = !string.IsNullOrWhiteSpace(request.TraceId);
            var fingerprint = StartAgentRunFingerprint.Compute(
                request.AgentName,
                command.Objective,
                traceIdSpecifiedInBody,
                request.TraceId);

            Func<Task<AgentRun>> startRun = () => handler.HandleAsync(command, cancellationToken);
            var outcome = await idempotencyService.ExecuteAsync(
                idempotencyKey,
                fingerprint,
                startRun,
                cancellationToken);

            if (outcome.IsConflict)
            {
                return Results.Conflict(new ApiErrorDto(
                    "IdempotencyKeyConflict",
                    "The Idempotency-Key was reused with a different request payload.",
                    requestTraceId,
                    null));
            }

            var run = outcome.Run!;
            return Results.Accepted($"/api/v1/agent-runs/{run.Id}", run.ToDto());
        }
    }

    var newRun = await handler.HandleAsync(command, cancellationToken);

    return Results.Accepted($"/api/v1/agent-runs/{newRun.Id}", newRun.ToDto());
})
.WithName("StartAgentRun")
.WithTags("AgentRuns")
.WithSummary("Starts a new deterministic agent run. Optional Idempotency-Key replays identical payloads to the same run; mismatched payloads return 409.")
.WithOpenApi();

v1.MapGet("/agent-runs/{runId:guid}", async (
    Guid runId,
    GetAgentRunQueryHandler handler,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var run = await handler.HandleAsync(runId, cancellationToken);
    if (run is null)
    {
        var traceId = httpContext.Response.Headers["X-Agentor-Trace-Id"].ToString();
        return Results.NotFound(new ApiErrorDto("RunNotFound", $"Agent run '{runId}' was not found.", traceId));
    }

    return Results.Ok(run.ToDto());
})
.WithName("GetAgentRun")
.WithTags("AgentRuns")
.WithSummary("Returns an agent run by ID.")
.WithOpenApi();

v1.MapGet("/agent-runs/{runId:guid}/trace", async (
    Guid runId,
    GetAgentRunTraceQueryHandler handler,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var trace = await handler.HandleAsync(runId, cancellationToken);
    if (trace is null)
    {
        var traceId = httpContext.Response.Headers["X-Agentor-Trace-Id"].ToString();
        return Results.NotFound(new ApiErrorDto("RunNotFound", $"Agent run '{runId}' was not found.", traceId));
    }

    return Results.Ok(trace.Select(t => t.ToDto()).ToList());
})
.WithName("GetAgentRunTrace")
.WithTags("AgentRuns")
.WithSummary("Returns execution trace events for an agent run.")
.WithOpenApi();

v1.MapGet("/agent-runs/{runId:guid}/steps", async (
    Guid runId,
    GetAgentRunStepsQueryHandler handler,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var steps = await handler.HandleAsync(runId, cancellationToken);
    if (steps is null)
    {
        var traceId = httpContext.Response.Headers["X-Agentor-Trace-Id"].ToString();
        return Results.NotFound(new ApiErrorDto("RunNotFound", $"Agent run '{runId}' was not found.", traceId));
    }

    return Results.Ok(steps.Select(s => s.ToDto()).ToList());
})
.WithName("GetAgentRunSteps")
.WithTags("AgentRuns")
.WithSummary("Returns steps for an agent run.")
.WithOpenApi();

v1.MapGet("/agent-runs/{runId:guid}/tool-calls", async (
    Guid runId,
    GetAgentRunToolCallsQueryHandler handler,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var toolCalls = await handler.HandleAsync(runId, cancellationToken);
    if (toolCalls is null)
    {
        var traceId = httpContext.Response.Headers["X-Agentor-Trace-Id"].ToString();
        return Results.NotFound(new ApiErrorDto("RunNotFound", $"Agent run '{runId}' was not found.", traceId));
    }

    return Results.Ok(toolCalls.Select(tc => tc.ToDto()).ToList());
})
.WithName("GetAgentRunToolCalls")
.WithTags("AgentRuns")
.WithSummary("Returns tool calls for an agent run (flattened across steps, ordered by step then occurrence).")
.WithOpenApi();

v1.MapGet("/agent-runs/{runId:guid}/manifest", async (
    Guid runId,
    GetRunManifestQueryHandler handler,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var manifest = await handler.HandleAsync(runId, cancellationToken);
    if (manifest is null)
    {
        var traceId = httpContext.Response.Headers["X-Agentor-Trace-Id"].ToString();
        return Results.NotFound(new ApiErrorDto("RunNotFound", $"Agent run '{runId}' was not found.", traceId));
    }

    return Results.Ok(manifest.ToDto());
})
.WithName("GetRunManifest")
.WithTags("AgentRuns")
.WithSummary("Returns the run manifest for a completed agent run.")
.WithOpenApi();

v1.MapGet("/agent-runs/{runId:guid}/athanor/latest-snapshot", async (
    Guid runId,
    GetLatestAthanorSnapshotForRunQueryHandler handler,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var outcome = await handler.HandleAsync(runId, cancellationToken);
    if (!outcome.RunExists)
    {
        var traceId = httpContext.Response.Headers["X-Agentor-Trace-Id"].ToString();
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
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    AthanorCanonicalLookupResult outcome;
    try
    {
        outcome = await handler.HandleAsync(runId, key, cancellationToken);
    }
    catch (ArgumentException ex)
    {
        var traceId = httpContext.Response.Headers["X-Agentor-Trace-Id"].ToString();
        return Results.BadRequest(new ApiErrorDto("ValidationError", ex.Message, traceId, [ex.Message]));
    }

    if (!outcome.RunExists)
    {
        var traceId = httpContext.Response.Headers["X-Agentor-Trace-Id"].ToString();
        return Results.NotFound(new ApiErrorDto("RunNotFound", $"Agent run '{runId}' was not found.", traceId));
    }

    if (outcome.Entry is null)
    {
        var traceId = httpContext.Response.Headers["X-Agentor-Trace-Id"].ToString();
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
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Query))
    {
        var traceId = httpContext.Response.Headers["X-Agentor-Trace-Id"].ToString();
        return Results.BadRequest(new ApiErrorDto("ValidationError", "Query is required.", traceId, ["Query is required."]));
    }

    var outcome = await handler.HandleAsync(runId, request.Query, cancellationToken);
    if (outcome is null)
    {
        var traceId = httpContext.Response.Headers["X-Agentor-Trace-Id"].ToString();
        return Results.NotFound(new ApiErrorDto("RunNotFound", $"Agent run '{runId}' was not found.", traceId));
    }

    if (outcome == false)
    {
        var traceId = httpContext.Response.Headers["X-Agentor-Trace-Id"].ToString();
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
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Summary))
    {
        var traceId = httpContext.Response.Headers["X-Agentor-Trace-Id"].ToString();
        return Results.BadRequest(new ApiErrorDto("ValidationError", "Summary is required.", traceId, ["Summary is required."]));
    }

    var outcome = await handler.HandleAsync(runId, request.Summary, request.PayloadJson ?? "{}", cancellationToken);
    if (outcome.Ok is null)
    {
        var traceId = httpContext.Response.Headers["X-Agentor-Trace-Id"].ToString();
        return Results.NotFound(new ApiErrorDto("RunNotFound", $"Agent run '{runId}' was not found.", traceId));
    }

    if (outcome.Ok == false)
    {
        var traceId = httpContext.Response.Headers["X-Agentor-Trace-Id"].ToString();
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
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    if (request.CandidateId == Guid.Empty)
    {
        var traceId = httpContext.Response.Headers["X-Agentor-Trace-Id"].ToString();
        return Results.BadRequest(new ApiErrorDto("ValidationError", "CandidateId is required.", traceId, null));
    }

    var actorId = request.ActorId is { } aid && aid != Guid.Empty
        ? aid
        : actorAccessor.Current.ActorId;

    if (actorId == Guid.Empty)
    {
        var traceId = httpContext.Response.Headers["X-Agentor-Trace-Id"].ToString();
        return Results.BadRequest(new ApiErrorDto("ValidationError", "ActorId is required (body or X-Agentor-Actor-Id).", traceId, null));
    }

    var outcome = await handler.HandleAsync(runId, request.CandidateId, actorId, cancellationToken);
    if (outcome is null)
    {
        var traceId = httpContext.Response.Headers["X-Agentor-Trace-Id"].ToString();
        return Results.NotFound(new ApiErrorDto("RunNotFound", $"Agent run '{runId}' was not found.", traceId));
    }

    if (outcome == false)
    {
        var traceId = httpContext.Response.Headers["X-Agentor-Trace-Id"].ToString();
        return Results.Conflict(new ApiErrorDto("RunNotRunning", "Review queue operations require a Running run.", traceId));
    }

    return Results.NoContent();
})
.WithName("QueueAthanorReview")
.WithTags("Athanor")
.WithSummary("Queues a candidate for Athanor human review and records the queue item on the run trace (non-canon). ActorId may be omitted when X-Agentor-Actor-Id is present.");

v1.MapPost("/agent-runs/{runId:guid}/human-review", async (
    Guid runId,
    ApplyHumanReviewRequestDto request,
    ApplyHumanReviewDecisionHandler handler,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
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
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
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

app.Run();

public partial class Program
{
}
