import pathlib
p = pathlib.Path(r"c:/dev/agentor/src/Agentor.Infrastructure/ToolExecutionPipeline.cs")
t = p.read_text(encoding="utf-8")
if "using Agentor.Application;" not in t:
    t = t.replace(
        "using Agentor.Application.Abstractions;",
        "using Agentor.Application;\nusing Agentor.Application.Abstractions;",
    )

anchor_try = """            try
            {
                var result = await executor.ExecuteAsync(request, attemptTimeout.Token).ConfigureAwait(false);"""
insert_started = """            try
            {
                if (attempt == 1 && ExternalAgentToolKeys.IsInvoke(request.ToolKey))
                {
                    run.RecordTrace(
                        TraceEventKind.ExternalAgentInvocationStarted,
                        "External-agent invocation started (non-canon).",
                        _clock.UtcNow,
                        ExternalAgentTraceDictionary(request));
                }

                var result = await executor.ExecuteAsync(request, attemptTimeout.Token).ConfigureAwait(false);"""

if anchor_try not in t:
    raise SystemExit("anchor try missing")
t = t.replace(anchor_try, insert_started)

anchor_success = """                if (result.Success)
                {
                    totalSw.Stop();
                    return new ToolPipelineExecutionResult(
                        true,
                        result.Output,
                        null,
                        ToolPipelineFailureKind.None,
                        attempt,
                        totalSw.Elapsed);
                }"""

insert_success = """                if (result.Success)
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
                }"""

if anchor_success not in t:
    raise SystemExit("anchor success missing")
t = t.replace(anchor_success, insert_success)

methods = """

    private static Dictionary<string, string> ExternalAgentTraceDictionary(ToolExecutionRequest request)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["toolKey"] = request.ToolKey,
            ["stepId"] = request.StepId.ToString(),
            ["runId"] = request.RunId.ToString(),
        };

        foreach (var kv in request.Input)
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
            foreach (var kv in result.Output)
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
"""

if "RecordExternalAgentCompletion" not in t:
    t = t.replace("\n}\n", methods + "\n}\n", 1)

p.write_text(t, encoding="utf-8")
print("pipeline patched")
