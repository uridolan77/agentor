# Agentor harness progress

## Phase 10 - Real service adapters + PR50.5 (2026-05-10)

Complete (**PR46-PR50**): `Agentor:Integrations` per-family modes **Fake / Http / Disabled**; HTTP adapters (`HttpKnowledgeStateClient`, `HttpModelGatewayClient`, `HttpMcpRegistryClient`, `HttpExternalAgentProtocolClient`); named integration `HttpClient` factories; **`GET /health`** liveness-only, **`GET /ready`**, **`GET /api/v1/integrations/status`** via `IntegrationSurfaceService`.

**PR50.5:** HTTP readiness probes fail unless `response.IsSuccessStatusCode`; **Disabled** integrations report `detail: "disabled"`; tests in `IntegrationEndpointsTests`, `IntegrationSurfaceServiceTests`, HTTP adapter stub tests; harness **phase 10** / **harnessPass PR50.5**.

## Phase 9 - External agent protocol adapters (2026-05-10)

Complete (PR41-PR45): `IExternalAgentProtocolClient` + `FakeExternalAgentProtocolClient` / `FakeA2AExternalAgentClient`; `external-agent.discover` / `external-agent.invoke` tools via `ToolRegistry.CreateDefault`; trace kinds + `ToolExecutionPipeline` external completion instrumentation; `SequentialAgentPlanExecutor` deny/review traces for external-agent tools; `RunManifest` **v1.2** with external-agent telemetry; `RunEvaluationHarness` snapshot external counts; `RunQualityGateEvaluator` optional `EXTERNAL_AGENT_OUTPUT_UNREVIEWED` warning; JSON fixtures under `tests/Agentor.Application.Tests/fixtures/eval/`. No real A2A/HTTP transports.

## Phase 8 - MCP, observability, and release (2026-05-10)

Complete (PR36-PR40): MCP port + fake registry + ToolRegistry binding; observability; Dockerfile / compose / CI / smoke script; v0.1.0-rc.1 versioning and roadmap note.

## Prior phases (summary)

- Phases 1-6 and PR30.5: completed per earlier harness entries (see `verification-log.md`).
- **Phase 7** - PR31-PR35 + PR35.5: skills, session memory, evaluation harness, quality gates.

## Next

**PR51** — Tenant / project / workspace identity model (Phase 11 entry). See `docs/planning/pr41-pr75/PR_INDEX_41_75.md`.
