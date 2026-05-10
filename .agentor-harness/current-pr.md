# Current PR — harness marker

Completed: **Phase 10 PR46–PR50** — integration modes (Fake / Http / Disabled), HTTP adapters for Athanor (`IKnowledgeStateClient`), Conexus (`IModelGatewayClient`), MCP (`IMcpRegistryClient`), and external agents (`IExternalAgentProtocolClient`), named `HttpClient` wiring, liveness `/health`, readiness `/ready`, and `GET /api/v1/integrations/status`.

Completed: **PR50.5** — Phase 10 harness alignment, readiness probe treats non-2xx HTTP as not ready, Disabled adapters report `detail: "disabled"`, API and Infrastructure tests for endpoints and HTTP stubs.

Next: **PR51** — Tenant / project / workspace identity model per `docs/planning/pr41-pr75/PR_INDEX_41_75.md` (Phase 11 groundwork — do not implement governance/auth queues in the same pass).
