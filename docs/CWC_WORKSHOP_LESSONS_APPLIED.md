# Anthropic CWC Workshop Lessons Applied to Agentor

## Lesson 1 — Do not build one giant agent

Agentor starts from execution primitives, not a giant prompt.

```text
AgentRun
AgentStep
ToolCall
PolicyDecision
ExecutionTraceEvent
RunManifest
```

## Lesson 2 — Decompose tools, skills, memory, evals, and subagents

Agentor separates:

```text
Tools        = executable capabilities
Skills       = procedural packages
Memory       = bounded runtime/session context
Athanor      = canonical knowledge state
Conexus      = model gateway
Evals        = measurable run quality and safety
Subagents    = later orchestration concept
```

## Lesson 3 — Skills are not tools

A tool executes. A skill guides or structures execution.

A skill may call tools, but it is not itself just an HTTP endpoint or MCP tool.

## Lesson 4 — Evals should arrive early

Agentor introduces evaluation fixtures earlier than deep evaluation infrastructure.

The PR1–PR40 plan includes:
- PR10: repository, integration, and eval fixture baseline
- PR34: full evaluation harness and regression fixtures
- PR35: quality gates and evaluation summaries

## Lesson 5 — Trace everything

Every meaningful action should be traceable:

```text
runId
stepId
toolCallId
policyDecisionId
traceId
manifest hash
external call reference
```

## Lesson 6 — External frameworks should not own the core

MCP, Microsoft Agent Framework, Semantic Kernel, A2A, LangGraph, AutoGen, and CrewAI are adapter candidates.

They do not define the core Agentor ontology.

## Lesson 7 — Coordination is a runtime layer, not a prompt trick

Agentor owns **coordination** as an explicit governed layer: topology, authority, how outputs aggregate, synchronization, termination, failure handling, budgets, and how coordination is **traced** and **evaluated**. Runtime policy after PR12 is necessary but only one slice of coordination. Canonical knowledge stays with Athanor; model execution stays with Conexus; frameworks stay behind adapters (see `docs/COORDINATION_LAYER.md`, ADR-008, and arXiv:2605.03310 note under `docs/papers/`).

Do not assume debate, consensus, or more agents are automatically better—compare coordination patterns using controlled evaluation and explicit signatures (cost, latency, tokens, diversity collapse, escalation, failure isolation, termination quality), not slogans.
