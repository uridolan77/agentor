using System.Diagnostics;
using Agentor.Application;
using Agentor.Application.Abstractions;
using Agentor.Domain;
using Agentor.Domain.Enums;
using Microsoft.Extensions.Options;

namespace Agentor.Infrastructure;

public sealed class ToolExecutionPipeline : IToolExecutionPipeline
{
    private readonly IClock _clock;
    private readonly ToolExecutionOptions _options;

    public ToolExecutionPipeline(IClock clock, IOptions<ToolExecutionOptions> options)
    {
        _clock = clock;
        _options = options.Value;
    }

    public async Task<ToolPipelineExecutionResult> ExecuteAsync(
        AgentRun run,
        Guid stepId,
        Guid toolCallId,
        IToolExecutor executor,
        ToolExecutionRequest request,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return new ToolPipelineExecutionResult(
                false,
                null,
                "Tool execution was canceled.",
                ToolPipelineFailureKind.Canceled,
                0,
                TimeSpan.Zero);
        }

        var timeoutMs = Math.Max(1, _options.TimeoutMilliseconds);
        var maxAttempts = Math.Max(1, _options.MaxAttempts);
        var retryDelayMs = Math.Max(0, _options.RetryDelayMilliseconds);

        var totalSw = Stopwatch.StartNew();

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var traceBase = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["stepId"] = stepId.ToString(),
                ["toolCallId"] = toolCallId.ToString(),
                ["toolKey"] = request.ToolKey,
                ["attempt"] = attempt.ToString(),
                ["maxAttempts"] = maxAttempts.ToString(),
                ["timeoutMs"] = timeoutMs.ToString()
            };

            run.RecordTrace(
                TraceEventKind.ToolExecutionAttemptStarted,
                $"Tool execution attempt {attempt} of {maxAttempts} started.",
                _clock.UtcNow,
                traceBase);

            using var attemptTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            attemptTimeout.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs));

            var attemptSw = Stopwatch.StartNew();

            try
            {
                if (attempt == 1 && ExternalAgentToolKeys.IsInvoke(request.ToolKey))
                {
                    run.RecordTrace(
                        TraceEventKind.ExternalAgentInvocationStarted,
                        "External-agent invocation started (non-canon).",
                        _clock.UtcNow,
                        ExternalAgentTraceDictionary(request));
                }

                var result = await executor.ExecuteAsync(request, attemptTimeout.Token).ConfigureAwait(false);
                attemptSw.Stop();

                var finishedData = new Dictionary<string, string>(traceBase, StringComparer.OrdinalIgnoreCase)
                {
                    ["durationMs"] = attemptSw.ElapsedMilliseconds.ToString(),
                    ["outcome"] = result.Success ? "success" : "executor_failed"
                };

                run.RecordTrace(
                    TraceEventKind.ToolExecutionAttemptFinished,
                    result.Success
                        ? $"Tool execution attempt {attempt} succeeded."
                        : $"Tool execution attempt {attempt} failed (executor reported failure).",
                    _clock.UtcNow,
                    finishedData);

                if (result.Success)
                {
                    RecordExternalAgentCompletion(run, request, result);
                    totalSw.Stop();
                    return new ToolPipelineExecutionResult(
                        true,
                        result.Output,
                        null,
                        ToolPipelineFailureKind.None,
                        attempt,
                        totalSw.Elapsed);
                }

                if (attempt < maxAttempts)
                {
                    var retryData = new Dictionary<string, string>(traceBase, StringComparer.OrdinalIgnoreCase)
                    {
                        ["nextAttempt"] = (attempt + 1).ToString(),
                        ["delayMs"] = retryDelayMs.ToString()
                    };
                    run.RecordTrace(
                        TraceEventKind.ToolCallRetrying,
                        $"Retrying tool execution after attempt {attempt} failure.",
                        _clock.UtcNow,
                        retryData);
                    if (retryDelayMs > 0)
                    {
                        await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
                    }

                    continue;
                }

                totalSw.Stop();
                return new ToolPipelineExecutionResult(
                    false,
                    result.Output,
                    result.ErrorMessage ?? "Tool execution failed.",
                    ToolPipelineFailureKind.ExecutorFailed,
                    attempt,
                    totalSw.Elapsed);
            }
            catch (OperationCanceledException)
            {
                attemptSw.Stop();

                if (cancellationToken.IsCancellationRequested)
                {
                    var cancelData = new Dictionary<string, string>(traceBase, StringComparer.OrdinalIgnoreCase)
                    {
                        ["durationMs"] = attemptSw.ElapsedMilliseconds.ToString(),
                        ["outcome"] = "canceled"
                    };

                    run.RecordTrace(
                        TraceEventKind.ToolExecutionAttemptFinished,
                        $"Tool execution attempt {attempt} canceled.",
                        _clock.UtcNow,
                        cancelData);

                    run.RecordTrace(
                        TraceEventKind.ToolCallCanceled,
                        "Tool execution canceled.",
                        _clock.UtcNow,
                        new Dictionary<string, string>(traceBase, StringComparer.OrdinalIgnoreCase)
                        {
                            ["attemptsUsed"] = attempt.ToString()
                        });

                    totalSw.Stop();
                    return new ToolPipelineExecutionResult(
                        false,
                        null,
                        "Tool execution was canceled.",
                        ToolPipelineFailureKind.Canceled,
                        attempt,
                        totalSw.Elapsed);
                }

                var timeoutData = new Dictionary<string, string>(traceBase, StringComparer.OrdinalIgnoreCase)
                {
                    ["durationMs"] = attemptSw.ElapsedMilliseconds.ToString(),
                    ["outcome"] = "timeout"
                };

                run.RecordTrace(
                    TraceEventKind.ToolExecutionAttemptFinished,
                    $"Tool execution attempt {attempt} timed out.",
                    _clock.UtcNow,
                    timeoutData);

                if (attempt < maxAttempts)
                {
                    var retryData = new Dictionary<string, string>(traceBase, StringComparer.OrdinalIgnoreCase)
                    {
                        ["nextAttempt"] = (attempt + 1).ToString(),
                        ["delayMs"] = retryDelayMs.ToString()
                    };
                    run.RecordTrace(
                        TraceEventKind.ToolCallRetrying,
                        $"Retrying tool execution after attempt {attempt} timed out.",
                        _clock.UtcNow,
                        retryData);
                    if (retryDelayMs > 0)
                    {
                        await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
                    }

                    continue;
                }

                run.RecordTrace(
                    TraceEventKind.ToolCallTimedOut,
                    "Tool execution timed out after all attempts.",
                    _clock.UtcNow,
                    new Dictionary<string, string>(traceBase, StringComparer.OrdinalIgnoreCase)
                    {
                        ["attemptsUsed"] = attempt.ToString()
                    });

                totalSw.Stop();
                return new ToolPipelineExecutionResult(
                    false,
                    null,
                    "Tool execution timed out.",
                    ToolPipelineFailureKind.Timeout,
                    attempt,
                    totalSw.Elapsed);
            }
            catch (Exception ex)
            {
                attemptSw.Stop();

                var finishedData = new Dictionary<string, string>(traceBase, StringComparer.OrdinalIgnoreCase)
                {
                    ["durationMs"] = attemptSw.ElapsedMilliseconds.ToString(),
                    ["outcome"] = "exception",
                    ["error"] = ex.Message
                };

                run.RecordTrace(
                    TraceEventKind.ToolExecutionAttemptFinished,
                    $"Tool execution attempt {attempt} raised an exception.",
                    _clock.UtcNow,
                    finishedData);

                if (attempt < maxAttempts)
                {
                    var retryData = new Dictionary<string, string>(traceBase, StringComparer.OrdinalIgnoreCase)
                    {
                        ["nextAttempt"] = (attempt + 1).ToString(),
                        ["delayMs"] = retryDelayMs.ToString()
                    };
                    run.RecordTrace(
                        TraceEventKind.ToolCallRetrying,
                        $"Retrying tool execution after attempt {attempt} exception.",
                        _clock.UtcNow,
                        retryData);
                    if (retryDelayMs > 0)
                    {
                        await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
                    }

                    continue;
                }

                totalSw.Stop();
                return new ToolPipelineExecutionResult(
                    false,
                    null,
                    ex.Message,
                    ToolPipelineFailureKind.ExecutorFailed,
                    attempt,
                    totalSw.Elapsed);
            }
        }

        throw new InvalidOperationException("Tool execution pipeline exited without producing a result.");
    }

    private static Dictionary<string, string> ExternalAgentTraceDictionary(ToolExecutionRequest request)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["toolKey"] = request.ToolKey,
            ["stepId"] = request.StepId.ToString(),
            ["runId"] = request.RunId.ToString(),
        };

        foreach (var kv in request.Input.ToPolicyEvaluationDictionary())
        {
            if (string.Equals(kv.Key, "protocolKind", StringComparison.OrdinalIgnoreCase)
                || string.Equals(kv.Key, "agentKey", StringComparison.OrdinalIgnoreCase)
                || string.Equals(kv.Key, "capabilityKey", StringComparison.OrdinalIgnoreCase))
            {
                d[kv.Key] = kv.Value;
            }
        }

        return d;
    }

    private void RecordExternalAgentCompletion(AgentRun run, ToolExecutionRequest request, ToolExecutionResult result)
    {
        if (!ExternalAgentToolKeys.IsExternalAgentTool(request.ToolKey))
        {
            return;
        }

        var data = ExternalAgentTraceDictionary(request);
        if (result.Output is not null)
        {
            foreach (var kv in result.Output.ToPolicyEvaluationDictionary())
            {
                if (string.Equals(kv.Key, "protocolKind", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(kv.Key, "agentKey", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(kv.Key, "capabilityKey", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(kv.Key, "invocationStatus", StringComparison.OrdinalIgnoreCase))
                {
                    data[kv.Key] = kv.Value;
                }
            }
        }

        if (ExternalAgentToolKeys.IsDiscover(request.ToolKey))
        {
            run.RecordTrace(
                TraceEventKind.ExternalAgentCapabilityDiscovered,
                "External-agent capabilities discovered (non-canon).",
                _clock.UtcNow,
                data);
            return;
        }

        if (ExternalAgentToolKeys.IsInvoke(request.ToolKey))
        {
            run.RecordTrace(
                TraceEventKind.ExternalAgentInvocationCompleted,
                "External-agent invocation completed (non-canon).",
                _clock.UtcNow,
                data);
        }
    }

}
