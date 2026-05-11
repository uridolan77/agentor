# Agentor v1 security review (Phase 38)

This document is an engineering security review for the v1.0 runtime surface. It states **evidence** (tests and code), **boundaries**, **residual risks**, and **production assumptions**. It is not a penetration test report or a formal certification.

## Evidence (automated)

| Area | Evidence |
|------|----------|
| Route → permission matrix (`Service` vs privileged writes, `HumanGovernanceApprover` / `System` samples) | `tests/Agentor.Api.Tests/AuthorizationMatrixApiTests.cs`, `AuthorizationMatrixApiFixture.cs`, `AuthorizationMatrixUnauthenticatedApiTests.cs` |
| Unauthenticated sampling under **Header** auth (no `X-Agentor-Actor-Id`) on selected `/api/v1/*` routes | `AuthorizationMatrixUnauthenticatedApiTests` |
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

- **SCOPE-001** (policy scope filtering for HTTP actors beyond run-scoped evaluation) remains deferred per `docs/RELEASE/v1.0-RC-DEFERRED-ITEMS.md`.
- **Matrix completeness**: new routes must update `docs/security/AUTHORIZATION_MATRIX.md` and extend `AuthorizationMatrixApiTests` / fixture seed data; drift is guarded by process, not by a single generated enumerator.
- **Readiness probe (`GET /ready`)**: automated Header-mode sampling in Phase 38 focused on `/api/v1/*` routes that match the matrix table-driven tests; operators should confirm ingress and probe configuration for `/ready` in their deployment (see `docs/security/auth-boundary.md` note).
- **Third-party adapters**: HTTP adapters redact common secret patterns; novel secret encodings may require catalog updates (`SensitiveFieldCatalog` / redaction policies).

## Related documents

- [`AUTHORIZATION_MATRIX.md`](./AUTHORIZATION_MATRIX.md)
- [`auth-boundary.md`](./auth-boundary.md)
- [`deployment-threat-notes.md`](./deployment-threat-notes.md)
- [`SECURITY_RELEASE_CHECKLIST.md`](./SECURITY_RELEASE_CHECKLIST.md)
