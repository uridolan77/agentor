using Agentor.Infrastructure.IntegrationStatus;
using Microsoft.AspNetCore.Http;

namespace Agentor.Api.Endpoints;

public static class SystemEndpoints
{
    public static WebApplication MapSystemEndpoints(this WebApplication app)
    {
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

        return app;
    }
}
