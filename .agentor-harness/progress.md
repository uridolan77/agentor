# Agentor harness progress

## Phase 9 — External agent protocol adapters (2026-05-10)

Complete (PR41–PR45): `IExternalAgentProtocolClient` + `FakeExternalAgentProtocolClient` / `FakeA2AExternalAgentClient`; `external-agent.discover` / `external-agent.invoke` tools via `ToolRegistry.CreateDefault`; trace kinds + `ToolExecutionPipeline` external completion instrumentation; `SequentialAgentPlanExecutor` deny/review traces for external-agent tools; `RunManifest` **v1.2** with external-agent telemetry; `RunEvaluationHarness` snapshot external counts; `RunQualityGateEvaluator` optional `EXTERNAL_AGENT_OUTPUT_UNREVIEWED` warning; JSON fixtures under `tests/Agentor.Application.Tests/fixtures/eval/`. No real A2A/HTTP transports.

## Phase 8 — MCP, observability, and release (2026-05-10)

Complete (PR36–PR40): MCP port + fake registry + ToolRegistry binding; observability; Dockerfile / compose / CI / smoke script; v0.1.0-rc.1 versioning and roadmap note.

## Prior phases (summary)

- Phases 1–6 and PR30.5: completed per earlier harness entries (see `verification-log.md`).
- **Phase 7** — PR31–PR35 + PR35.5: skills, session memory, evaluation harness, quality gates.

## Next

Phase 10 planning per `docs/planning/pr41-pr75/PR_INDEX_41_75.md` (post external-agent foundation).
