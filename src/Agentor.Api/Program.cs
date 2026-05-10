using System.Text.Json.Serialization;
using Agentor.Api.Mapping;
using Agentor.Api.Middleware;
using Agentor.Application;
using Agentor.Application.Commands;
using Agentor.Application.Options;
using Agentor.Application.Queries;
using Agentor.Application.Validation;
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

v1.MapPost("/agent-runs", async (
    StartAgentRunRequestDto request,
    StartAgentRunHandler handler,
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

    var run = await handler.HandleAsync(command, cancellationToken);

    return Results.Accepted($"/api/v1/agent-runs/{run.Id}", run.ToDto());
})
.WithName("StartAgentRun")
.WithTags("AgentRuns")
.WithSummary("Starts a new deterministic agent run.")
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
