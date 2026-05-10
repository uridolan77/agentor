# Agent Instructions for Agentor

## Project identity

Agentor is a deterministic, observable, policy-governed agent execution runtime.

Agentor is not:
- a chatbot product
- a generic RAG system
- a canonical knowledge engine
- a replacement for Athanor
- an LLM gateway
- an MCP marketplace
- a wrapper around Microsoft Agent Framework, Semantic Kernel, LangGraph, AutoGen, CrewAI, or A2A

## External service boundaries

- Athanor remains the canonical knowledge-state and provenance service.
- Conexus remains the LLM/model gateway.
- MCP integration is a future transport/tooling layer.
- A2A / external-agent protocols are post-v0.1 adapter concerns.
- Microsoft Agent Framework and Semantic Kernel may be adapter integrations later, not Agentor core.

## Anthropic CWC-derived development doctrine

Agentor follows the CWC decomposition lesson:

```text
Do not build one giant agent.
Build a runtime made of:
- runs
- steps
- tools
- skills
- memory boundaries
- policy decisions
- traces
- manifests
- evals
- external adapters
```

## PR style

Use medium-long, coherent, reviewable PR passes.

A PR should be large enough to introduce one meaningful runtime layer with tests and docs, but small enough to review without mixing unrelated service boundaries.

Do not implement multiple future phases in one PR.

## General development doctrine

1. Prefer deterministic tests before abstractions.
2. Every run must be traceable.
3. Every tool call must be policy-evaluated.
4. Tool output is evidence or candidate material, never canon.
5. Skills are not tools.
6. Memory is not Athanor.
7. Evals are a first-class runtime concern, not a late afterthought.
8. Do not introduce infrastructure before the domain needs it.
9. Do not add agent taxonomies before the run kernel is stable.
10. Keep frameworks behind adapters.

## PR1 limitations

Allowed:
- AgentProfile
- AgentRun
- AgentStep
- ToolCall
- PolicyDecision
- ExecutionTrace
- RunManifest
- FakeToolExecutor
- AllowAllPolicyEvaluator
- InMemoryAgentRunRepository
- Minimal API endpoints

Forbidden in PR1:
- real Athanor client
- real Conexus client
- real MCP
- Microsoft Agent Framework
- Semantic Kernel
- A2A
- LangGraph / AutoGen / CrewAI
- LLM calls
- memory subsystem
- vector database
- background jobs
- distributed orchestration
- dashboard
- authentication beyond simple future docs
