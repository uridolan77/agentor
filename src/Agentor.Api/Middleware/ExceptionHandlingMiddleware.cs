using System.Net;
using System.Text.Json;
using Agentor.Application.Abstractions;
using Agentor.Application.Orchestration;
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
        catch (AgentRunPersistenceConcurrencyException ex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Conflict;
            context.Response.ContentType = "application/json";

            var traceId = context.Response.Headers.TryGetValue(TraceIdHeaderName, out var tv)
                ? tv.ToString()
                : null;

            var errorDto = new ApiErrorDto(
                "AgentRunPersistenceConcurrency",
                ex.Message,
                traceId);
            var payload = JsonSerializer.Serialize(errorDto, _jsonOptions);
            await context.Response.WriteAsync(payload);
        }
        catch (AgentRunTraceImmutabilityException ex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            context.Response.ContentType = "application/json";

            var traceId = context.Response.Headers.TryGetValue(TraceIdHeaderName, out var tv)
                ? tv.ToString()
                : null;

            var errorDto = new ApiErrorDto(
                "AgentRunTraceImmutability",
                ex.Message,
                traceId);
            var payload = JsonSerializer.Serialize(errorDto, _jsonOptions);
            await context.Response.WriteAsync(payload);
        }
        catch (RunOrchestrationValidationException ex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            context.Response.ContentType = "application/json";

            var traceId = context.Response.Headers.TryGetValue(TraceIdHeaderName, out var tv)
                ? tv.ToString()
                : null;

            var errorDto = new ApiErrorDto(
                "RunOrchestrationValidationError",
                "Run start routing is invalid.",
                traceId,
                ex.Errors.ToList());

            var payload = JsonSerializer.Serialize(errorDto, _jsonOptions);
            await context.Response.WriteAsync(payload);
        }
        catch (RunOrchestrationNotFoundException ex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            context.Response.ContentType = "application/json";

            var traceId = context.Response.Headers.TryGetValue(TraceIdHeaderName, out var tv)
                ? tv.ToString()
                : null;

            var errorDto = new ApiErrorDto(ex.ReasonCode, ex.Message, traceId);
            var payload = JsonSerializer.Serialize(errorDto, _jsonOptions);
            await context.Response.WriteAsync(payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled Agentor API error.");

            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";

            var traceId = context.Response.Headers.TryGetValue(TraceIdHeaderName, out var tv)
                ? tv.ToString()
                : null;

            var errorDto = new ApiErrorDto(
                "AgentorUnhandledError",
                "An unexpected error occurred.",
                traceId);
            var payload = JsonSerializer.Serialize(errorDto, _jsonOptions);

            await context.Response.WriteAsync(payload);
        }
    }
}
