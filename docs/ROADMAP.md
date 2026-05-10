# Agentor Roadmap

## PR1 — Runtime foundation

- deterministic fake run
- policy decision shape
- tool call shape
- trace shape
- manifest shape
- in-memory repository
- minimal API

## PR2 — Athanor client boundary

- define `IKnowledgeStateClient`
- add `Agentor.Athanor.Client`
- support read-only latest snapshot/search evidence stubs
- no canonization

## PR3 — Conexus client boundary

- define `IModelGatewayClient`
- call Conexus through abstraction
- no provider SDKs in Agentor

## PR4 — Tool registry

- ToolDefinition registry
- tool execution dispatch
- policy per tool
- no MCP yet

## PR5 — MCP adapter

- MCP tool discovery
- capability negotiation
- external tool binding

## PR6 — Evaluation

- run-level quality/safety/cost/latency evaluation
- regression fixtures

## PR7 — Agent planning

- recipe / plan model
- multi-step deterministic workflows

## PR8 — UI or dashboard

Only after runtime + traces + manifests are stable.
