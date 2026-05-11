# Security release checklist (Phase 36 / PR147)

Use this list before tagging a release candidate or publishing images. It summarizes auth, exposure, and secret-handling evidence already implemented in code and tests.

## Authentication modes

| Mode | When to use | Evidence |
|------|-------------|----------|
| **Fake** | Local / automated tests only | `AgentorFakeAuthenticationHandler`, `AgentorAuthOptionsValidator` blocks outside Development/Test unless `AllowFakeOutsideDevelopment` |
| **Header** | Service mesh or trusted ingress that injects actor id | `AgentorHeaderAuthenticationHandler`, `HeaderOrFakeActorAccessor` |
| **Jwt** | Production-style identity | `JwtBearer` when `JwtAuthority` is set; see `docs/security/auth-boundary.md` |

## JWT: unvalidated bearer path (dangerous)

`JwtAcceptUnvalidatedBearerTokens` (without `JwtAuthority`) registers **`Agentor.JwtUnvalidated`**, which parses bearer tokens **without signature validation**. That is appropriate only for **trusted lab / gateway** scenarios.

- **Production / Staging** hosts refuse this combination unless `JwtAllowUnvalidatedTokensOutsideDevelopment=true` (explicit escape hatch). See `AgentorAuthOptionsValidator` and **`OpenApiExposureApiTests`** / startup validation tests in `AgentorAuthOptionsValidatorTests`.
- Documented in **`docs/security/auth-boundary.md`** (this checklist does not replace that file).

## Header mode limitations

- ASP.NET authentication requires a **valid GUID** in `X-Agentor-Actor-Id` (or configured header name).
- The issued role is **`HumanOperator`** only; **`HumanGovernanceApprover`** requires **JWT** (or a future header-role extension). See **`AUTHORIZATION_MATRIX.md`** header note and **`RoleBasedAuthorizationDecisionService`**.

## Secrets and redaction

Automated tests (non-exhaustive but required gates):

| Area | Tests |
|------|-------|
| JSON key redaction for audit-style payloads | `tests/Agentor.Application.Tests/Redaction/JsonRedactionTests.cs`, `RedactionPolicyTests.cs` |
| Integration error strings | `tests/Agentor.Infrastructure.Tests/IntegrationHttpErrorTests.cs` |
| Audit export nested redaction | `tests/Agentor.Application.Tests/GetRunAuditExportQueryHandlerTests.cs` |
| Smoke report export | `tests/Agentor.Infrastructure.Tests/IntegrationSmokeTests.cs` (`IntegrationSmokeReportWriterTests` / redaction) |

## OpenAPI exposure

- Default: document route in **Development** and **Test**/**Testing** environments.
- **Production**: off unless `Agentor:OpenApi:Enabled=true`. Confirmed by **`OpenApiExposureApiTests`**.

## OpenAPI contract snapshot

Checked-in document and drift test: **`docs/api/API_CONTRACT_SNAPSHOT.md`**.
