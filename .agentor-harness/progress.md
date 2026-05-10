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

Phase 7 (PR31-PR35 + PR35.5) completed; see Phase 7 sections below.

## Phase 7 - Skills, memory, evaluation (2026-05-10)

PR31-PR35 completed in one harness session: skill package domain, skill plan execution + catalog, session scratch memory with EF column, deterministic evaluation harness helper, run quality gate summary.

| PR | Scope | Status |
|----|--------|--------|
| PR31 | SkillPackage model | Done |
| PR32 | Skill invocation pipeline + traces | Done |
| PR33 | Session memory boundary + persistence | Done |
| PR34 | Evaluation harness | Done |
| PR35 | Run quality gates | Done |

## Phase 7 hardening - PR35.5 (2026-05-10)

Granular harness acceptance for PR31-PR35; RunEvaluationHarness JSON regression fixture; stronger RunQualityGateEvaluator; skill audit trace assertions; docs/SESSION_MEMORY_BOUNDARY.md; PlanInputBuilder extracted from SequentialAgentPlanExecutor.

| Pass | Scope | Status |
|------|--------|--------|
| PR35.5 | Harness + tests + docs + small executor helper | Done |

Next: Phase 8 per docs/planning/pr1-pr40/PR_INDEX.md when scheduled (not started here).
