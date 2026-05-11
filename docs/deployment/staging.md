# Staging deployment

Staging should mirror **production semantics** (auth, persistence, workers) at smaller scale, using non-production credentials and data.

## PostgreSQL

- Provision a **dedicated** Postgres instance or schema for staging.
- Set `AgentorPersistence:Mode=Postgres` and a **staging-only** connection string via environment variables or a secret store (not committed JSON).
- Apply EF migrations during a maintenance window: see `docs/developer/MIGRATION_AND_UPGRADE.md`.

## Auth mode selection

Choose under `Agentor:Auth:Mode`:

| Mode | When |
|------|------|
| **Jwt** | Preferred when an OIDC authority exists; set `JwtAuthority` (+ optional `JwtAudience`). |
| **Header** | Trusted mesh / gateway injects `X-Agentor-Actor-Id` (GUID). Remember **HumanGovernanceApprover** is not representable in Header mode today — use Jwt for governance approver flows. |
| **Fake** | Blocked outside Development/Test unless `AllowFakeOutsideDevelopment=true` (avoid in real staging unless explicitly required for harness-style tests). |

Details: `docs/security/auth-boundary.md`, `docs/security/AUTHORIZATION_MATRIX.md`.

## OpenAPI gating

- **Production-like** `ASPNETCORE_ENVIRONMENT=Staging` hosts **do not** map `/openapi/v1.json` unless `Agentor:OpenApi:Enabled=true`.
- Keep OpenAPI **off** for internet-exposed staging unless you intend to publish the contract publicly.

## Workers and outbox

For realistic queue behavior:

- `Agentor:RunQueue:ExecutionMode` — use durable/EF-backed settings consistent with `docs/REPO_TRUTH.md` (queue store + worker).
- `Agentor:RunWorker:Enabled=true` with sane `PollIntervalMilliseconds` / `LeaseTtlSeconds`.
- `Agentor:OutboxDispatch:Enabled=true` with a **real sink** in staging (not `NoOp` outside dev without the explicit escape hatch documented in tests and `SECURITY_RELEASE_CHECKLIST.md`).

## Integration endpoints

Under `Agentor:Integrations`, set **Http** modes and base URLs for Athanor, Conexus, MCP, and external agents as needed. Use staging credentials and TLS.

## Secret management

- Inject secrets via **environment variables**, **Key Vault**, or your platform’s secret object — not checked-in `appsettings.json`.
- Rotate staging credentials independently from production.

## Verification

Run **`docs/RELEASE/v1.0-RC-VERIFICATION.md`** against the staging URL after deploy. Include **`scripts/release-smoke.ps1`** where practical.

## See also

- `docs/deployment/production.md` — hardening and operational defaults.
- `docs/operator/runbook.md`.
