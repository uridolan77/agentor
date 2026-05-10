# Current PR - harness marker

Completed: **Phase 10 PR46-PR50** - integration modes (Fake / Http / Disabled), HTTP adapters for Athanor (`IKnowledgeStateClient`), Conexus (`IModelGatewayClient`), MCP (`IMcpRegistryClient`), and external agents (`IExternalAgentProtocolClient`), named `HttpClient` wiring, liveness `/health`, readiness `/ready`, and `GET /api/v1/integrations/status`.

Completed: **PR50.5** - Phase 10 harness alignment, readiness probe treats non-2xx HTTP as not ready, Disabled adapters report `detail: "disabled"`, API and Infrastructure tests for endpoints and HTTP stubs.

Completed: **Phase 11 PR51-PR55** - governance identifiers on `AgentRun` / start DTO, `PolicyProfileRules` via `RuntimePolicyOptions.ActiveProfile`, human review decisions and resume path, `ICurrentActorAccessor` + `X-Agentor-Actor-Id`, deterministic audit export with SHA-256 and key-name redaction, Athanor handlers using `ResolveAthanorProjectId()`, persistence migration for scope + human review JSON.

Completed: **PR55.5** - Phase 11 harness reconciliation: `feature-list.json` phase **11** / harnessPass **PR55.5**, granular acceptance rows, expanded automated tests, `docs/GOVERNANCE_BOUNDARY.md`, verification log refresh.

Completed: **Phase 12 PR56-PR60** - background run queue (`IRunQueue`, in-memory worker, `POST/GET .../agent-runs/queued`), durable outbox (`OutboxDispatcher`, EF + in-memory stores), execution leases and distributed operation ledger (EF + in-memory), HTTP transport resilience (`TransportResilienceRegistry`, delegating handler), EF persistence and migration `20260512080000_Phase12Reliability`.

Completed: **PR60.5** - Phase 12 harness reconciliation: `feature-list.json` phase **12** / harnessPass **PR60.5**; `OutboxDispatcherTests`, `Phase12EfRoundTripTests` (Sqlite), `TransportResilienceRegistryTests`; `EfDistributedOperationLedger` clears change tracker after successful insert; verification log refresh.

Completed: **PR60.6** - Phase 12 reliability hardening before Phase 13: `ResilientIntegrationDelegatingHandler` clones `HttpRequestMessage` per attempt with buffered POST bodies; `ResilientIntegrationDelegatingHandlerTests` (POST JSON retries, max attempts, non-retryable status, circuit-open short-circuit); harness note punctuation cleanup; session-handoff clarifies in-memory queue vs durable queue and outbox worker scope.

Completed: **Phase 13 PR61-PR65** - Product and operator surface under `/api/v1`: management stores and endpoints for recipes, plans (from recipe), skill packages, policy profiles; run read aliases (`/runs/{id}/timeline`, `coordination-view`, `audit-packet`); read-only operator dashboard DTO; human-review inbox aliases (`/reviews/pending`, `/reviews/{id}/decisions`); wiring in `Phase13ProductEndpoints.cs`, query handlers, in-memory management DI.

Completed: **PR65.5** - Phase 13 harness reconciliation: `feature-list.json` phase **13** / harnessPass **PR65.5**; Phase 13 acceptance rows; `Phase13ProductSurfaceApiTests` extended for reviews pending and review decision conflict; docs `docs/api/phase13-product-surface.md`, `docs/operator/dashboard-and-inbox.md`, `docs/developer/phase13-workflows.md`, `docs/examples/phase13-workflows.md`; verification log refresh.

Completed: **Phase 14 PR66-PR70** - Advanced evaluation: `EvaluationFixtureRegistry` (schema 4), harness fixture JSON parse, `CoordinationEvaluationProfile` + `HarnessProfileMaterializer`, `QualityRuleSetEvaluator` (JSON, built-in predicates), `CoordinationEvaluationMetrics`, `EvaluationReportGenerator` (Markdown/JSON/CSV, CI folder); tests under `tests/Agentor.Application.Tests/Evaluation/`; fixtures under `tests/Agentor.Application.Tests/fixtures/eval/`.

Completed: **PR70.5** - Phase 14 harness reconciliation: `feature-list.json` phase **14** / harnessPass **PR70.5**; Phase 14 acceptance rows; `docs/developer/phase14-evaluation.md`; verification log refresh.

Next: **Phase 15** when opened per `docs/planning/pr41-pr75/` (do not implement until that scope is explicitly requested).
