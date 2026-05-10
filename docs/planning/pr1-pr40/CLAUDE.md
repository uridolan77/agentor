# CLAUDE.md — Agentor PR1–PR40 Build Rules

## Project identity

Agentor is a deterministic, observable, policy-governed .NET runtime for agent execution.

## CWC doctrine

Do not build one giant agent. Build decomposed runtime layers:

```text
runs
plans
steps
tools
skills
memory boundaries
policy decisions
execution traces
run manifests
evals
external adapters
```

## Framework doctrine

External frameworks are adapters, not Agentor core.

Do not let MCP, Microsoft Agent Framework, Semantic Kernel, A2A, LangGraph, AutoGen, or CrewAI define Agentor Domain.

## Service doctrine

Agentor executes. Athanor canonizes. Conexus routes models. MCP connects tools later.

## Verification

Every PR must end with:

```powershell
dotnet restore
dotnet build
dotnet test
```
