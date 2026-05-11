# Deployment Threat Notes (Phase 19 Auth)

This note captures operational risks and deployment controls for Agentor auth modes.

## Threats

1. Fake mode in production

- Risk: any caller is effectively treated as a trusted operator.
- Control: startup validation blocks Fake mode outside Development/Test unless explicitly overridden.

2. Header spoofing in Header mode

- Risk: clients can forge actor identity header if perimeter trust is weak.
- Control: only use Header mode behind a trusted gateway that rewrites/locks identity headers.

3. Invalid JWT claim mapping

- Risk: actor id or role cannot be resolved correctly, leading to access confusion.
- Control: configure claim mappings explicitly and treat unresolved actor id or role as unauthorized.

4. Alias endpoint authorization bypass

- Risk: alias endpoints can bypass canonical endpoint permission checks if not guarded consistently.
- Control: apply the same endpoint authorization checks to canonical and alias routes (`/agent-runs/*` and `/runs|/reviews` aliases).

5. Over-privileged service actors

- Risk: machine identities may perform governance mutations or read operator runtime surfaces.
- Control: default role mapping keeps `Service` as read-only in `RoleBasedAuthorizationDecisionService` and excludes `OpsRead`.

6. Audit and operations data exposure

- Risk: unauthorized actors retrieving audit exports or operational queue/outbox/lease state.
- Control: `GET /audit-export` requires `AuditRead`; `/api/v1/ops/*` and **`GET /api/v1/operator/dashboard`** require `OpsRead`.

7. No-op outbox dispatch in production

- Risk: enabled dispatch with `NoOpOutboxSink` marks outbox messages succeeded without delivery.
- Control: `OutboxHostedService` throws outside Development/Test when `OutboxDispatch:Enabled=true` and sink is no-op unless `OutboxDispatch:AllowNoOpSinkOutsideDevelopment=true` is explicitly set.

8. Trusted ingress assumptions

- Risk: reverse proxies, service meshes, and API gateways must align with the configured **Auth** mode. Misconfiguration can expose Header-mode spoofing or bypass intended JWT validation.
- Control: document ingress ownership; prefer **Jwt** with `JwtAuthority` in production; terminate TLS and client trust at the edge.

9. OpenAPI document exposure

- Risk: the OpenAPI JSON document can describe internal surface area if exposed on production URLs.
- Control: Production maps `/openapi/v1.json` only when `Agentor:OpenApi:Enabled=true` (see `Program.cs`, `OpenApiExposureApiTests`).

10. Integration smoke and Athanor write probes

- Risk: automated smoke tools that perform writes against real backends can corrupt environments if pointed at production-like targets.
- Control: keep smoke defaults disabled; gate write paths behind explicit configuration and operator runbooks (`docs/operator/integration-smoke.md`, `docs/operator/release-smoke.md`).

11. Diagnostics and structured logging leak paths

- Risk: operator bundles, dashboards, or logs could echo payloads, tokens, or connection strings.
- Control: diagnostics builder avoids secrets; ops DTOs sanitize errors; `ObservabilityRedaction` and JSON redaction policies back audit/eval exports (see `docs/security/v1-security-review.md`).

12. Queue / outbox operational abuse

- Risk: queue flood, lease starvation, or outbox retry storms can impact availability even when authz is correct.
- Control: durable queue/outbox implementations enforce worker identity on state transitions; rate limits and ingress controls are deployment responsibilities.

## Deployment recommendations

1. Prefer `Jwt` mode for production.
2. Keep `Fake` mode disabled in production unless under explicit break-glass procedure.
3. If using `Header` mode, terminate trust at a single ingress and strip inbound identity headers from external clients.
4. Pin and review claim mappings as part of release validation.
5. Include authz endpoint tests for both canonical and alias routes in CI gates.

## Out of scope

- Identity provider onboarding details (tenant apps, consent flows, secret rotation) are deployment-specific and intentionally out of repository scope.
- Policy scope filtering (`SCOPE-001`) remains deferred to v1.1.
