# Agentor PR41–PR75 Index

This index extends the original PR1–PR40 roadmap after the v0.1 release-candidate foundation.

## Phase 9 — External agent and protocol adapter layer

- **PR41 — External agent protocol abstraction**  
  Add a generic `IExternalAgentProtocolClient` boundary and deterministic fake implementation. No real A2A/ACP/network transport.

- **PR42 — Fake A2A-style adapter**  
  Model A2A-like discovery and invocation behind the generic external-agent protocol port. Fake only.

- **PR43 — External-agent tool binding**  
  Register external-agent discovery/invocation as policy-gated Agentor tools.

- **PR44 — External-agent audit and provenance surfaces**  
  Expose external-agent invocation metadata in traces, manifests, read models, and audit views.

- **PR45 — External-agent evaluation fixtures**  
  Add deterministic evaluation fixtures for fake external-agent invocation flows.

## Phase 10 — Real service adapters and integration modes

- **PR46 — Integration mode configuration**  
  Add explicit Fake/Http/Disabled modes for Athanor, Conexus, MCP, and ExternalAgents.

- **PR47 — Real Athanor HTTP client adapter**  
  Implement `HttpKnowledgeStateClient` behind `IKnowledgeStateClient`. No canonization API.

- **PR48 — Real Conexus HTTP client adapter**  
  Implement `HttpModelGatewayClient` behind `IModelGatewayClient`. No provider SDKs in Agentor.

- **PR49 — Real MCP transport adapter**  
  Add an Infrastructure-only MCP transport adapter behind the Phase 8 MCP registry boundary.

- **PR50 — Integration health and readiness endpoints**  
  Add health/readiness and integration-status surfaces without exposing secrets.

## Phase 11 — Governance, tenancy, security, and human review

- **PR51 — Tenant/project/workspace identity model**  
  Introduce `TenantId`, `WorkspaceId`, `ProjectId`, `KnowledgeScopeId`, and `ActorId` concepts.

- **PR52 — Policy bundles and policy profiles**  
  Move beyond flat runtime options into versioned policy bundles/profiles.

- **PR53 — Human review workflow v1**  
  Add review requests, review decisions, approval/rejection, and governed resume.

- **PR54 — Actor/auth boundary**  
  Add actor-context abstraction and local fake actor. No full identity provider yet.

- **PR55 — Deterministic audit export**  
  Export complete run audit packets with deterministic hash and redaction boundaries.

## Phase 12 — Durable execution and reliability

- **PR56 — Background run queue**  
  Add queue/worker boundary for asynchronous run execution.

- **PR57 — Durable outbox**  
  Add outbox messages and dispatcher for external side effects.

- **PR58 — Distributed idempotency and execution leases**  
  Add durable idempotency, run locks, and execution leases.

- **PR59 — Transport resilience policies**  
  Add retry/backoff/circuit-breaker options for HTTP adapters only.

- **PR60 — Persistence hardening**  
  Persist Phase 9–12 entities with EF mappings, migrations, and round-trip tests.

## Phase 13 — Product/API and operator surface

- **PR61 — Recipe/plan/skill management APIs**  
  Add management endpoints for recipes, plans, skill packages, and policy profiles.

- **PR62 — Run timeline and replay APIs**  
  Add deterministic timeline, coordination view, and audit-packet endpoints.

- **PR63 — Operator dashboard shell**  
  Add read-only operator shell or API-ready dashboard DTOs.

- **PR64 — Human review inbox API/UI**  
  Add review inbox surfaces and governed review actions.

- **PR65 — Documentation and examples**  
  Add product, operator, API, and developer examples.

## Phase 14 — Advanced evaluation and coordination science

- **PR66 — Evaluation fixture registry**  
  Formalize versioned evaluation datasets, fixtures, cases, and expected snapshots.

- **PR67 — Coordination profile evaluation**  
  Compare coordination profiles under controlled model/tool/prompt/context conditions.

- **PR68 — Quality rule set DSL-lite**  
  Add declarative, built-in quality rules without arbitrary code execution.

- **PR69 — Coordination evaluation metrics**  
  Add reliability, resolution, cost, latency, review burden, and failure-isolation metrics.

- **PR70 — Evaluation report generator**  
  Generate deterministic Markdown/JSON/CSV evaluation reports suitable for CI artifacts.

## Phase 15 — v1.0 platform hardening

- **PR71 — Security and secret hygiene audit**  
  Add redaction policy and tests for logs, traces, manifests, audit packets, and exports.

- **PR72 — Performance and load baseline**  
  Add benchmark/load smoke baselines and regression thresholds.

- **PR73 — CI/CD release pipeline v1**  
  Harden restore/build/test/migration/Docker/eval/release artifact pipeline.

- **PR74 — Upgrade and migration readiness**  
  Add DTO compatibility tests, migration checklist, and contract versioning policy.

- **PR75 — v1.0 release candidate**  
  Final architecture-boundary review, docs, release notes, and remaining harness closure/defer decisions.

## Cross-phase invariants

```text
External protocols are tools/adapters, not Agentor ontology.
Framework SDK types never enter Domain.
Tool execution remains policy-gated.
RequiresReview never executes automatically.
Session memory remains run-scoped scratch.
Athanor remains the only canonical knowledge authority.
Conexus remains the only model gateway.
Provider SDKs do not enter Agentor core.
Evaluation fixtures must be deterministic.
Every phase updates feature-list.json with item-level acceptance checks.
```
