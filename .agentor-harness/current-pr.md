# Current PR — harness marker

Completed: **Phase 10 PR46–PR50** — integration modes (Fake / Http / Disabled), HTTP adapters for Athanor (`IKnowledgeStateClient`), Conexus (`IModelGatewayClient`), MCP (`IMcpRegistryClient`), and external agents (`IExternalAgentProtocolClient`), named `HttpClient` wiring, liveness `/health`, readiness `/ready`, and `GET /api/v1/integrations/status`.

Completed: **PR50.5** — Phase 10 harness alignment, readiness probe treats non-2xx HTTP as not ready, Disabled adapters report `detail: "disabled"`, API and Infrastructure tests for endpoints and HTTP stubs.

Completed: **Phase 11 PR51–PR55** — governance identifiers on `AgentRun` / start DTO, `PolicyProfileRules` via `RuntimePolicyOptions.ActiveProfile`, human review decisions and resume path, `ICurrentActorAccessor` + `X-Agentor-Actor-Id`, deterministic audit export with SHA-256 and key-name redaction, Athanor handlers using `ResolveAthanorProjectId()`, persistence migration for scope + human review JSON.

Completed: **PR55.5** — Phase 11 harness reconciliation: `feature-list.json` phase **11** / harnessPass **PR55.5**, granular acceptance rows, expanded automated tests, `docs/GOVERNANCE_BOUNDARY.md`, verification log refresh.

Next: **PR56** — Background run queue (Phase 12 entry — do not implement until PR56 scope is opened).
