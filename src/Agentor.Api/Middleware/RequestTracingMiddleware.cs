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

        await _next(context);
    }
}
