# PR1 Build Plan — Agentor Runtime Foundation

## Goal

Create a minimal .NET 9 runtime foundation that can execute one deterministic fake agent run.

## Build

- `Agentor.Domain`
- `Agentor.Application`
- `Agentor.Contracts`
- `Agentor.Infrastructure`
- `Agentor.Api`
- unit tests

## API

```text
GET  /health
POST /agent-runs
GET  /agent-runs/{runId}
GET  /agent-runs/{runId}/manifest
```

## Definition of Done

- solution restores
- solution builds
- tests pass
- `/health` returns ok
- `POST /agent-runs` creates a completed deterministic run
- run has at least one step
- run has a policy decision
- run has a tool call
- run has trace events
- manifest endpoint returns reproducible summary
- docs state that Athanor remains external

## Forbidden in PR1

- real Athanor client
- real Conexus client
- real MCP
- model calls
- memory subsystem
- vector database
- background jobs
- distributed orchestration
- dashboard
