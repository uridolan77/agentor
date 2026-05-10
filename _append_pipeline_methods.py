import pathlib
p = pathlib.Path(r"c:/dev/agentor/src/Agentor.Infrastructure/ToolExecutionPipeline.cs")
t = p.read_text(encoding="utf-8")
if "RecordExternalAgentCompletion" in t and "private void RecordExternalAgentCompletion" not in t:
    methods = r'''
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
'''
    old = '        throw new InvalidOperationException("Tool execution pipeline exited without producing a result.");\n    }\n}\n'
    new = '        throw new InvalidOperationException("Tool execution pipeline exited without producing a result.");\n    }\n' + methods + '\n}\n'
    if old not in t:
        raise SystemExit('tail pattern missing')
    t = t.replace(old, new, 1)
    p.write_text(t, encoding='utf-8')
    print('methods appended')
else:
    print('skip')
