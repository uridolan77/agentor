using Agentor.Api.Mapping;
using Agentor.Api.Middleware;
using Agentor.Application;
using Agentor.Application.Commands;
using Agentor.Application.Queries;
using Agentor.Contracts;
using Agentor.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAgentorApplication();
builder.Services.AddAgentorInfrastructure();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestTracingMiddleware>();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "Agentor.Api",
    version = "0.1.0"
}));

app.MapPost("/agent-runs", async (
    StartAgentRunRequestDto request,
    StartAgentRunHandler handler,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Objective))
    {
        return Results.BadRequest(new { error = "Objective is required." });
    }

    var traceId = string.IsNullOrWhiteSpace(request.TraceId)
        ? httpContext.Response.Headers["X-Agentor-Trace-Id"].ToString()
        : request.TraceId;

    var run = await handler.HandleAsync(
        new StartAgentRunCommand(request.AgentName, request.Objective, traceId),
        cancellationToken);

    return Results.Accepted($"/agent-runs/{run.Id}", run.ToDto());
});

app.MapGet("/agent-runs/{runId:guid}", async (
    Guid runId,
    GetAgentRunQueryHandler handler,
    CancellationToken cancellationToken) =>
{
    var run = await handler.HandleAsync(runId, cancellationToken);
    return run is null ? Results.NotFound() : Results.Ok(run.ToDto());
});

app.MapGet("/agent-runs/{runId:guid}/manifest", async (
    Guid runId,
    GetRunManifestQueryHandler handler,
    CancellationToken cancellationToken) =>
{
    var manifest = await handler.HandleAsync(runId, cancellationToken);
    return manifest is null ? Results.NotFound() : Results.Ok(manifest.ToDto());
});

app.Run();

public partial class Program
{
}
