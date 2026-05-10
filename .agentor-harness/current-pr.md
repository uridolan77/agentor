# Current PR — harness marker

Completed: **Phase 10 PR46–PR50** — integration modes (Fake / Http / Disabled), HTTP adapters for Athanor (`IKnowledgeStateClient`), Conexus (`IModelGatewayClient`), MCP (`IMcpRegistryClient`), and external agents (`IExternalAgentProtocolClient`), named `HttpClient` wiring, liveness `/health`, readiness `/ready`, and `GET /api/v1/integrations/status`.

Completed: **PR50.5** — Phase 10 harness alignment, readiness probe treats non-2xx HTTP as not ready, Disabled adapters report `detail: "disabled"`, API and Infrastructure tests for endpoints and HTTP stubs.

Completed: **Phase 11 PR51–PR55** — governance identifiers on `AgentRun` / start DTO, `PolicyProfileRules` via `RuntimePolicyOptions.ActiveProfile`, human review decisions and resume path, `ICurrentActorAccessor` + `X-Agentor-Actor-Id`, deterministic audit export with SHA-256 and key-name redaction, Athanor handlers using `ResolveAthanorProjectId()`, persistence migration for scope + human review JSON.

Completed: **PR55.5** — Phase 11 harness reconciliation: `feature-list.json` phase **11** / harnessPass **PR55.5**, granular acceptance rows, expanded automated tests, `docs/GOVERNANCE_BOUNDARY.md`, verification log refresh.

Completed: **Phase 12 PR56–PR60** — background run queue (`IRunQueue`, in-memory worker, `POST/GET .../agent-runs/queued`), durable outbox (`OutboxDispatcher`, EF + in-memory stores), execution leases and distributed operation ledger (EF + in-memory), HTTP transport resilience (`TransportResilienceRegistry`, delegating handler), EF persistence and migration `20260512080000_Phase12Reliability`.

Completed: **PR60.5** — Phase 12 harness reconciliation: `feature-list.json` phase **12** / harnessPass **PR60.5**; `OutboxDispatcherTests`, `Phase12EfRoundTripTests` (Sqlite), `TransportResilienceRegistryTests`; `EfDistributedOperationLedger` clears change tracker after successful insert; verification log refresh.

Next: **Phase 13** — Product operator surface (`docs/planning/pr41-pr75/PHASE_13_PRODUCT_OPERATOR_SURFACE.md`) — do not implement until that scope is opened.
