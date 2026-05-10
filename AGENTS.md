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

## Current architecture boundaries

These boundaries describe the **implemented** runtime shape (core plus adapters), not a future-only roadmap sketch.

- **Agentor coordinates**: runs, steps, tools, skills (declared packages), policy decisions, traces, manifests, eval hooks, and integration ports.
- **Athanor canonizes**: knowledge state and provenance live in Athanor when integrated; Agentor treats Athanor ports as **non-canon by default** unless explicitly configured as authoritative for a deployment.
- **Conexus routes models**: LLM traffic stays behind Conexus-shaped adapters when present.
- **MCP is an adapter surface**: MCP appears as registry/client/tool-binding ports (including fake/in-memory and HTTP-oriented adapters), not as core domain logic.
- **External-agent / A2A-style protocols** may surface via generic or protocol-shaped adapters; core stays protocol-agnostic.
- **Frameworks stay outside Domain**: Semantic Kernel, Microsoft Agent Framework, and similar stacks belong in Infrastructure/Application adapters, not in `Agentor.Domain`.
- **Tool execution remains policy-gated**: every tool invocation flows through policy evaluation and trace emission.
- **RequiresReview is distinct from Deny**: review queues and human decisions are modeled separately from hard denials.

## Historical PR1 constraints (frozen reference)

PR1 was an early harness milestone with deliberate omissions so tests could run without external services. It is **historical**, not a blanket rule for all future work.

**Historical PR1 scope allowed**: minimal contracts, fake executor, allow-all policy, in-memory repository, minimal API.

**Historical PR1 forbade** (for that milestone): real Athanor/Conexus/MCP, LLM calls, vector stores, background orchestration products, dashboards, etc.

New phases may introduce integrations **when explicitly scheduled**, subject to architecture boundaries above—not PR1-era prohibition lists.

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

## Agentor Session Closeout Protocol — mandatory

A task is not complete when code compiles. A task is complete only when code, tests, harness state, verification evidence, and handoff are reconciled.

For every PR, phase, or multi-step task, complete the closeout protocol before giving a final response.

### Definition of Done

The task is done only when all of the following are true:

1. `dotnet restore Agentor.sln` succeeds.
2. `dotnet build Agentor.sln --no-restore` succeeds.
3. `dotnet test Agentor.sln --no-build` succeeds.
4. `.agentor-harness/current-pr.md` is updated.
5. `.agentor-harness/feature-list.json` is valid JSON, UTF-8, and updated with item-level acceptance rows.
6. `.agentor-harness/progress.md` is updated.
7. `.agentor-harness/verification-log.md` contains the exact commands run, results, and test counts.
8. `.agentor-harness/session-handoff.md` states:
   - what was completed,
   - what is next,
   - what was explicitly not started,
   - remaining risks or false acceptance items.
9. All new or edited `.cs`, `.json`, `.md`, `.yml`, `.yaml`, `.ps1`, and `.sln` files are UTF-8 text, not UTF-16/null-byte encoded.
10. The next PR/phase was not started unless explicitly requested.

### Required final self-audit

Before the final response, re-open these files from disk and verify their contents:

- `.agentor-harness/current-pr.md`
- `.agentor-harness/feature-list.json`
- `.agentor-harness/progress.md`
- `.agentor-harness/verification-log.md`
- `.agentor-harness/session-handoff.md`

The final response must quote or summarize the actual re-read values:

- current completed phase/PR
- next phase/PR
- `feature-list.json` phase
- `feature-list.json` harnessPass
- latest test count from `verification-log.md`
- explicit confirmation that the next phase was not started

If any harness file is stale, malformed, missing, invalid JSON, UTF-16 encoded, or inconsistent with the code, fix it before reporting completion.

### Harness truth rule

The harness is the project memory. Never leave it stale.

If code says Phase N is complete but the harness still says Phase N-1, the task is incomplete.

If tests pass but the harness is wrong, the task is incomplete.

If the harness says an acceptance item passes but there is no named evidence, the task is incomplete.

If an acceptance item is not verified, leave it as `passes: false` with a clear evidence/TODO string.

### Scope rule

Do not start the next PR or phase during closeout.

If implementation accidentally includes work from a later PR, document it honestly in the handoff and feature list. Do not pretend it did not happen.

### Encoding rule

All repository text files must be UTF-8. Cursor/editor write paths sometimes create UTF-16 files. Detect and fix this before completion.

Null bytes in fetched/rendered file content mean the file is malformed for this repository.

### Harness verification command

After updating harness files, run:

```powershell
pwsh ./scripts/verify-harness.ps1
pwsh ./scripts/verify-repo-clean.ps1
```

For phase-specific closeout, pass expected values:

```powershell
pwsh ./scripts/verify-harness.ps1 -ExpectedPhase 26 -ExpectedHarnessPass PR117
```

See `.agentor-harness/SESSION_CLOSEOUT_PROTOCOL.md` for the canonical closeout procedure.