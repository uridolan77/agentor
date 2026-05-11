# Agentor v1 security review (Phase 38)

This document is an engineering security review for the v1.0 runtime surface. It states **evidence** (tests and code), **boundaries**, **residual risks**, and **production assumptions**. It is not a penetration test report or a formal certification.

**PR158.5 (documentation)**: Clarifies that **SCOPE-001** is **closed** (Phase 26 / PR117 per `docs/RELEASE/v1.0-RC-DEFERRED-ITEMS.md`); distinguishes that closure from the separate HTTP-actor boundary below; and states authorization-matrix automation coverage accurately (see **Residual risks** and [`AUTHORIZATION_MATRIX.md`](./AUTHORIZATION_MATRIX.md)).

## Evidence (automated)

| Area | Evidence |
|------|----------|
| Route → permission matrix — **`Service`** role table-driven (forbidden vs allowed reads); **`HumanGovernanceApprover`**, **`System`**, and unauthenticated **Header** mode **sampled** (not an exhaustive full role × route product) | `tests/Agentor.Api.Tests/AuthorizationMatrixApiTests.cs`, `AuthorizationMatrixApiFixture.cs`, `AuthorizationMatrixUnauthenticatedApiTests.cs` |
| Unauthenticated sampling under **Header** auth (no `X-Agentor-Actor-Id`) on selected `/api/v1/*` routes (`GET /ready` excluded — see residual risks) | `AuthorizationMatrixUnauthenticatedApiTests` |
| Production blocks **Fake** auth without explicit override | `tests/Agentor.Api.Tests/ProductionAuthSafeDefaultsApiTests.cs`, `AgentorAuthOptionsValidator` |
| JWT unvalidated-bearer escape hatch outside Development/Test | `AgentorAuthOptionsValidator`, `AgentorAuthOptionsValidatorTests`, `OpenApiExposureApiTests` |
| OpenAPI document disabled in Production by default | `Program.cs`, `OpenApiExposureApiTests` |
| No-op outbox sink blocked in Production when dispatch enabled | `OutboxHostedService` + `OutboxHostedServiceTests` |
| Audit export and nested JSON redaction | `GetRunAuditExportQueryHandlerTests`, `JsonRedactionTests`, `RedactionPolicyTests` |
| Ops queue/outbox error fields redacted | `IntegrationEndpointsTests.OpsEndpoints_ReturnReadOnlyStatusWithoutSecrets` |
| Integration HTTP error strings | `IntegrationHttpErrorTests` |
| Structured log / exception message sanitization | `ObservabilityRedactionTests` |
| Diagnostics bundle (JSON + Markdown) — no connection strings | `IntegrationEndpointsTests` (diagnostics JSON + markdown), `OperatorDiagnosticsService` |

## Boundaries

- **Identity**: ASP.NET authentication establishes `HttpContext.User`; **Agentor** permissions use `ICurrentActorAccessor` + `IAuthorizationDecisionService`. Header mode issues a **HumanOperator** principal only; **HumanGovernanceApprover** requires JWT (or equivalent) with a recognized role claim.
- **Authorization**: `EndpointAuthorization.Require` returns **401** when actor resolution fails and **403** when the role lacks the mapped `AgentorPermission`.
- **Secrets**: Tool payloads, raw upstream bodies, and full connection strings are not operator diagnostics outputs; persistence is summarized as **configured / not configured** only.

## Production assumptions

1. **Ingress is trusted** for identity: in **Header** mode, the perimeter must strip or overwrite client-supplied actor headers; otherwise identity can be forged.
2. **Jwt** mode uses `JwtAuthority` for normal production validation, or a deliberately narrow escape hatch (`JwtAcceptUnvalidatedBearerTokens`) with `JwtAllowUnvalidatedTokensOutsideDevelopment` only when a gateway has already validated tokens.
3. **Fake** auth is for Development/Test unless `AllowFakeOutsideDevelopment` is explicitly set under operational control.
4. **Workers** (`RunWorker`, outbox dispatch, integration smoke write paths) remain **off** unless explicitly enabled in configuration suitable for the environment.

## Residual risks (honest)

- **HTTP actor vs run-scoped policy scope**: ASP.NET authentication and `ICurrentActorAccessor` do **not** add a separate tenant/workspace/project **authorization** layer on top of policy evaluation. **Policy bundle rules are filtered and merged by `AgentRunScope`**; that work was tracked historically as **SCOPE-001** and is **closed in Phase 26 (PR117)** — see `docs/RELEASE/v1.0-RC-DEFERRED-ITEMS.md` and `docs/security/auth-boundary.md` (scope note). This is a product boundary statement, not an open deferral.
- **Matrix completeness**: new routes must update `docs/security/AUTHORIZATION_MATRIX.md` and extend `AuthorizationMatrixApiTests` / fixture seed data. **`Service`** expectations are table-driven; other roles and unauthenticated paths are **sampled**. Full generated role × route coverage is **future hardening**, not claimed here.
- **Readiness probe (`GET /ready`)**: Phase 38 **Header**-mode unauthenticated tests intentionally targeted `/api/v1/*` samples only. Under **`WebApplicationFactory`** + **Header** mode, `GET /ready` without the actor header did **not** reliably produce **401** in the same harness configuration as `/api/v1/agent-runs`, so it was **excluded** from automated assertions to avoid a flaky or misleading gate. Operators must still align probes with their **Auth** mode in real deployments (see `docs/security/auth-boundary.md`).
- **Third-party adapters**: HTTP adapters redact common secret patterns; novel secret encodings may require catalog updates (`SensitiveFieldCatalog` / redaction policies).

## Related documents

- [`AUTHORIZATION_MATRIX.md`](./AUTHORIZATION_MATRIX.md)
- [`auth-boundary.md`](./auth-boundary.md)
- [`deployment-threat-notes.md`](./deployment-threat-notes.md)
- [`SECURITY_RELEASE_CHECKLIST.md`](./SECURITY_RELEASE_CHECKLIST.md)
