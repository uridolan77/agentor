# ADR-006 — External Frameworks Are Adapters, Not Agentor Core

## Status

Accepted

## Context

Agentor may eventually need to interoperate with MCP, Microsoft Agent Framework, Semantic Kernel, A2A, LangGraph, AutoGen, CrewAI, or other agent frameworks.

These frameworks are useful, but each carries its own ontology of agents, tools, memory, messages, plans, and state.

If Agentor adopts one of these as its core, Agentor loses its stable runtime identity.

## Decision

Agentor will not build its runtime ontology on top of Microsoft Agent Framework, Semantic Kernel, LangGraph, AutoGen, CrewAI, MCP, or A2A.

Agentor may integrate with these systems through adapter projects once the core runtime model is stable.

Agentor's internal primitives remain:

```text
AgentRun
AgentPlan
AgentStep
ToolCall
PolicyDecision
ExecutionTraceEvent
RunManifest
SkillInvocation
EvaluationResult
```

## Consequences

- Frameworks enter through Infrastructure adapters.
- Domain remains framework-independent.
- Model calls still go through Conexus.
- Canonical knowledge still goes through Athanor.
- MCP tools still go through Agentor ToolRegistry and PolicyEvaluator.
- A2A remains post-v0.1 unless a concrete product requirement appears.
- Coordination semantics remain Agentor-owned; see ADR-008 and `docs/COORDINATION_LAYER.md` (vendor orchestration graphs are not the coordination ontology).
