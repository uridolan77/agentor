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

Domain must not reference:
- HTTP
- EF Core
- JSON serialization concerns
- provider SDKs
- Athanor HTTP client
- Conexus HTTP client
- MCP client
- Microsoft Agent Framework
- Semantic Kernel
- A2A libraries

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

## CWC decomposition model

Agentor applies the Anthropic CWC workshop lesson by decomposing agent systems into separable runtime parts:

```text
AgentRun        = execution instance
AgentPlan       = structured execution plan
ToolCall        = deterministic or external action
SkillInvocation = procedural guidance / reusable capability
SessionMemory   = bounded context
PolicyDecision  = runtime authorization/safety decision
ExecutionTrace  = audit trail
RunManifest     = reproducible summary
Evaluation      = measurable quality/safety/cost outcome
Adapter         = external system boundary
```

The goal is not to build a giant agent prompt. The goal is to build a system where tools, skills, memory, traces, policies, evals, and adapters are independently testable.

## Framework strategy

External frameworks are adapters, not core.

Allowed later through Infrastructure adapters:
- MCP
- Microsoft Agent Framework
- Semantic Kernel
- A2A
- LangGraph
- AutoGen
- CrewAI

Not allowed in core Domain/Application:
- framework-specific agent types
- provider-specific model calls
- external framework state machines
- direct tool execution outside Agentor policy

## PR1 execution mode

PR1 uses:
- `AllowAllPolicyEvaluator`
- `FakeToolExecutor`
- `InMemoryAgentRunRepository`

This is intentional. PR1 proves the kernel before introducing real services.
