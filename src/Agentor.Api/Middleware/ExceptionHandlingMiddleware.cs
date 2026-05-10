using System.Net;
using System.Text.Json;
using Agentor.Contracts;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace Agentor.Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private const string TraceIdHeaderName = "X-Agentor-Trace-Id";

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IOptions<JsonOptions> jsonOptions)
    {
        _next = next;
        _logger = logger;
        _jsonOptions = jsonOptions.Value.SerializerOptions;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled Agentor API error.");

            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";

            var traceId = context.Response.Headers.TryGetValue(TraceIdHeaderName, out var tv)
                ? tv.ToString()
                : null;

            var errorDto = new ApiErrorDto("AgentorUnhandledError", ex.Message, traceId);
            var payload = JsonSerializer.Serialize(errorDto, _jsonOptions);

            await context.Response.WriteAsync(payload);
        }
    }
}
