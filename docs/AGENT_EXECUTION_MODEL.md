# Agent Execution Model

## PR1 model

```text
AgentProfile
  ↓
AgentRun
  ↓
AgentStep
  ↓
PolicyDecision
  ↓
ToolCall
  ↓
ExecutionTraceEvent
  ↓
RunManifest
```

## State transitions

### AgentRunStatus

```text
Queued → Running → Completed
Queued → Running → Failed
```

### AgentStepStatus

```text
Pending → Running → Completed
Pending → Running → Failed
```

### ToolCallStatus

```text
Pending → Running → Succeeded
Pending → Running → Failed
Pending → Denied
```

## Invariant

A tool call must not execute before a policy decision.
