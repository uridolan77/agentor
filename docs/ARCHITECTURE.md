# Agentor Architecture

Agentor follows the same service-architecture discipline as `conexus.adaptation`:

```text
Api
Application
Domain
Infrastructure
Contracts
Tests
```

## Dependency rule

```text
Api → Application + Contracts + Infrastructure
Infrastructure → Application + Domain
Application → Domain
Contracts → Domain only if needed
Domain → nothing
```

## Runtime loop

```text
StartAgentRunCommand
  → load/create AgentProfile
  → create AgentRun
  → create AgentStep
  → evaluate policy
  → execute tool
  → record ToolCall
  → record ExecutionTraceEvent
  → complete AgentRun
  → persist run
  → expose RunManifest
```

## PR1 execution mode

PR1 uses:
- `AllowAllPolicyEvaluator`
- `FakeToolExecutor`
- `InMemoryAgentRunRepository`

This is intentional. PR1 proves the kernel before introducing real services.
