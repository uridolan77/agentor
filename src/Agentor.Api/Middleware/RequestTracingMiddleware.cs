using System.Diagnostics;
using Agentor.Api.Observability;

namespace Agentor.Api.Middleware;

public sealed class RequestTracingMiddleware
{
    private const string HeaderName = "X-Agentor-Trace-Id";
    private readonly RequestDelegate _next;

    public RequestTracingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var traceId = context.Request.Headers.TryGetValue(HeaderName, out var incoming)
            && !string.IsNullOrWhiteSpace(incoming.ToString())
                ? incoming.ToString()
                : Guid.NewGuid().ToString("N");

        context.Items[HeaderName] = traceId;
        context.Response.Headers[HeaderName] = traceId;

        using var activity = AgentorDiagnostics.ActivitySource.StartActivity("http.request", ActivityKind.Server);
        activity?.SetTag("agentor.trace_id", traceId);
        activity?.SetTag("http.request.method", context.Request.Method);
        activity?.SetTag("http.route", context.GetEndpoint()?.DisplayName ?? context.Request.Path.Value);

        AgentorDiagnostics.HttpServerRequestCount.Add(
            1,
            new KeyValuePair<string, object?>("method", context.Request.Method));

        await _next(context);
    }
}
