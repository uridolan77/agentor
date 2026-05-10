import pathlib
p = pathlib.Path(r"c:/dev/agentor/src/Agentor.Infrastructure/ExternalAgents/ExternalAgentInvokeToolExecutor.cs")
t = p.read_text(encoding="utf-8")
t = t.replace(
    'return new ToolExecutionResult(false, null, "agentKey is required.");',
    'return new ToolExecutionResult(false, new Dictionary<string, string>(), "agentKey is required.");',
)
t = t.replace(
    'return new ToolExecutionResult(false, null, "capabilityKey is required.");',
    'return new ToolExecutionResult(false, new Dictionary<string, string>(), "capabilityKey is required.");',
)
t = t.replace(
    'return new ToolExecutionResult(false, null, result.ErrorMessage ?? "External agent invocation failed.");',
    'return new ToolExecutionResult(false, new Dictionary<string, string>(), result.ErrorMessage ?? "External agent invocation failed.");',
)
p.write_text(t, encoding="utf-8")
