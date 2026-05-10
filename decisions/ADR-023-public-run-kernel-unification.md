# ADR-023 — Public run API must use a runtime orchestration kernel

## Status

Accepted

## Context

Agentor implements a real governed execution stack (plans, tools, policy, traces, manifests, review, queue). The public **`POST /api/v1/agent-runs`** entry point historically encoded **PR1 fake-tool** behavior directly, which makes external behavior look less capable than internal architecture.

## Decision

The **public run API must not** remain the long-term home for **hard-coded PR1 fake-tool** orchestration. It must call a **single runtime orchestration kernel** (or thin façade over it) that can route execution modes explicitly (including a **named legacy / fixture** mode).

**Fake or deterministic harness execution** remains valid as a **selectable adapter or test profile**, not as the implied default product story.

## Consequences

- New orchestration types and handler refactors align with this seam (Phase 24 implemented `IAgentRunOrchestrator` and public `/agent-runs` routing; see `src/Agentor.Application/Orchestration/`).
- Documentation and harness state track current vs. remaining gaps (`docs/REPO_TRUTH.md`, root `README.md`, `.agentor-harness/`).

## Related

- `docs/REPO_TRUTH.md`
- ADR-001 (Agentor is a runtime), ADR-004 (PR1 external dependencies)
