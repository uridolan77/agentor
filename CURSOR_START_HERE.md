# Cursor Start Here — Agentor PR1

You are working in the `uridolan77/agentor` repository.

The existing repo is effectively empty. Replace the stub content with this starter package.

## Goal

Create PR1: `Agentor runtime foundation`.

This PR must produce a compiling .NET 9 solution with:

- Domain/Application/Contracts/Infrastructure/Api split
- deterministic fake agent run
- no external service dependency
- no Athanor call yet
- no Conexus call yet
- no MCP
- append-only-style run trace
- run manifest endpoint
- unit tests

## Hard boundary

Agentor executes. Athanor canonizes. Conexus routes LLMs.

Do not add code that lets Agentor directly:
- accept knowledge object versions
- resolve contradictions
- create canonical snapshots
- bypass project-local authority
- treat tool output as canonical truth

## First Cursor instruction

After extracting this package into the repo, ask Cursor:

```text
Review this PR1 Agentor foundation package. First, check whether the solution compiles conceptually. Then run dotnet restore/build/test. Fix only compile/test issues. Do not add new product features. Keep PR1 limited to deterministic AgentRun execution with FakeToolExecutor, PolicyDecision, ExecutionTrace, and RunManifest.
```

## Second Cursor instruction

```text
Compare the current implementation against docs/PR1_BUILD_PLAN.md and decisions/ADR-001-agentor-is-agent-runtime.md. Make only the smallest changes required to satisfy PR1. Do not introduce Athanor, Conexus, MCP, memory, vector search, background jobs, or real LLM calls.
```
