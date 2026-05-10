# ADR-007 — Anthropic CWC Decomposition Doctrine

## Status

Accepted

## Context

Agent systems become brittle when implemented as a single giant prompt with many tools and unclear runtime state.

The Anthropic CWC workshop pattern emphasizes decomposition into skills, tools, memory, evals, subagents, code execution, and observable traces.

## Decision

Agentor adopts this decomposition as design doctrine.

Agentor will model and test separately:

```text
runs
steps
tools
skills
memory boundaries
policy decisions
execution traces
run manifests
evaluation results
external adapters
```

## Consequences

- Tools are executable capabilities.
- Skills are procedural packages.
- Memory is bounded runtime context and not canonical knowledge.
- Evals appear early and mature over time.
- Subagents are not part of PR1 and should not be introduced before the plan/execution model is stable.
