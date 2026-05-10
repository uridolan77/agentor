# Agentor

Agentor is a deterministic, observable, policy-governed .NET runtime for agent execution.

It is not a chatbot framework, not a knowledge-state engine, and not an LLM gateway.

## Service boundaries

```text
Agentor = agent execution runtime
Athanor = canonical knowledge-state / provenance service
Conexus = LLM/model gateway
MCP    = later tool/protocol connectivity layer
```

Agentor may execute plans, invoke tools, record traces, produce manifests, and submit candidates to Athanor.

Agentor must not canonize knowledge, resolve contradictions, create canonical snapshots, or treat tool/LLM output as truth.

## PR1 scope

PR1 proves only the runtime kernel:

```text
AgentProfile
→ AgentRun
→ AgentStep
→ FakeToolCall
→ PolicyDecision
→ ExecutionTrace
→ RunManifest
```

No Athanor call.
No Conexus call.
No MCP.
No real tools.
No memory subsystem.
No multi-agent orchestration.

## Quick start

```powershell
dotnet restore
dotnet build
dotnet test

dotnet run --project src/Agentor.Api
```

Smoke test:

```powershell
./scripts/pr1-smoke.ps1
```

## API surface in PR1

```text
GET  /health
POST /agent-runs
GET  /agent-runs/{runId}
GET  /agent-runs/{runId}/manifest
```

## Architecture source

Agentor adopts the service architecture discipline of `conexus.adaptation`:

```text
Domain
Application
Infrastructure
Api
Contracts
Tests
```

But its domain is not adaptation. Its domain is agent execution.
