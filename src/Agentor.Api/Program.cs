using System.Text.Json.Serialization;
using Agentor.Api.Mapping;
using Agentor.Api.Middleware;
using Agentor.Application;
using Agentor.Application.Commands;
using Agentor.Application.Options;
using Agentor.Application.Queries;
using Agentor.Application.Services;
using Agentor.Application.Validation;
using Agentor.Api;
using Agentor.Domain;
using Agentor.Contracts;
using Agentor.Infrastructure;
using Agentor.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAgentorApplication();
builder.Services.AddAgentorInfrastructure();

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

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestTracingMiddleware>();

app.MapOpenApi();

app.MapGet("/health", (IOptions<AgentorRuntimeOptions> runtimeOpts) => Results.Ok(new
{
    status = "ok",
    service = runtimeOpts.Value.ServiceName,
    version = runtimeOpts.Value.Version
}))
.WithName("GetHealth")
.WithTags("System")
.WithSummary("Returns the health status of the Agentor API.");

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

    var command = new StartAgentRunCommand(request.AgentName, request.Objective, commandTraceId);
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

app.Run();

public partial class Program
{
}
