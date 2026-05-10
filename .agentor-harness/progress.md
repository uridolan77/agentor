# Agentor harness - progress

## Phase 5 - Athanor integration (2026-05-10)

PR21-PR25 delivered the Athanor port, fake, handlers, API routes, and guards.

PR25.5 (same phase, cleanup): item-level feature-list.json, extra tests, boundary doc updates. No Conexus.

| PR | Scope | Status |
|----|--------|--------|
| PR21 | IKnowledgeStateClient, Contracts DTOs, FakeKnowledgeStateClient, DI | Done |
| PR22 | Read-only snapshot + canonical lookup, query handlers, GET API | Done |
| PR23 | Evidence provenance on run trace | Done |
| PR24 | Candidate submission trace | Done |
| PR25 | Review queue trace + non-canonization guards | Done |
| PR25.5 | Harness itemization + tests + docs | Done |

## Phase 6 — Conexus integration (2026-05-10)

PR26–PR30: Conexus port (`IModelGatewayClient`), `FakeModelGatewayClient`, `conexus.model-complete` tool (`ModelGatewayToolExecutor`), prompt/model profile refs on Conexus DTOs and tool I/O, declared budget gates on model-call tool input, run manifest v1.1 with aggregated Conexus model-call telemetry.

| PR | Scope | Status |
|----|--------|--------|
| PR26 | Port + fake gateway + DI | Done |
| PR27 | Model-call tool through gateway | Done |
| PR28 | Prompt/model profile contract | Done |
| PR29 | Cost/latency budget policy | Done |
| PR30 | Model-call telemetry in manifests | Done |
| PR30.5 | Conexus/manifest boundary + declared budget docs/tests | Done |

Next: PR31 Skills / Phase 7 (not started; PR30.5 completed first).
