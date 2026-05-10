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

## External service boundaries

- Athanor remains the canonical knowledge-state and provenance service.
- Conexus remains the LLM/model gateway.
- MCP integration is a future transport/tooling layer.

## Development doctrine

1. Keep PRs small and vertical.
2. Prefer deterministic tests before abstractions.
3. Every run must be traceable.
4. Every tool call must be policy-evaluated.
5. Tool output is evidence or candidate material, never canon.
6. Do not introduce infrastructure before the domain needs it.
7. Do not add agent taxonomies before the run kernel is stable.

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
- LLM calls
- memory subsystem
- vector database
- background jobs
- distributed orchestration
- dashboard
- authentication beyond simple future docs
