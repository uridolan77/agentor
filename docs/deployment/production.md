# Production deployment

This guide summarizes **production-relevant** configuration for Agentor **Api**. It is not a cloud-specific runbook; adapt ingress, TLS, and secret stores to your platform.

## PostgreSQL

- **First-class** path: `AgentorPersistence:Mode=Postgres` with a HA Postgres deployment.
- Run **`dotnet ef database update`** (or your migration job) from CI-tested assemblies only; verify with `dotnet ef migrations list` matching the deployed build.
- See **`docs/developer/MIGRATION_AND_UPGRADE.md`** for backup, restore, rollback, and queue/outbox cautions.

## Auth mode selection

- **Default recommendation:** **`Jwt`** with **`JwtAuthority`** set to your OIDC issuer; configure claim mappings per `docs/security/auth-boundary.md`.
- **`JwtAcceptUnvalidatedBearerTokens`** without **`JwtAuthority`** is a **trusted-gateway / lab** path only; **Production**/**Staging** hosts refuse startup unless **`JwtAllowUnvalidatedTokensOutsideDevelopment=true`** is explicitly set (dangerous).
- **`Header`** mode is acceptable when a trusted ingress injects actor GUIDs; governance approver flows still require **Jwt** today.

Validate with **`ProductionAuthSafeDefaultsApiTests`** and **`AgentorAuthOptionsValidator`** behavior described in **`docs/security/SECURITY_RELEASE_CHECKLIST.md`**.

## OpenAPI gating

- In **Production**, OpenAPI route mapping is **off** unless **`Agentor:OpenApi:Enabled=true`** after full configuration merge.
- Keep disabled for internet-facing deployments unless you explicitly want public schema export.

## Workers and outbox

- Enable **`Agentor:RunWorker:Enabled`** when using durable queued execution; tune poll interval and lease TTL for your DB latency and desired claim fairness.
- Enable **`Agentor:OutboxDispatch:Enabled`** with a **real outbox sink**; **`NoOp`** sink is blocked outside Development/Test unless **`OutboxDispatch:AllowNoOpSinkOutsideDevelopment=true`** (explicit escape hatch).

## Integration endpoint configuration

Configure **`Agentor:Integrations`** HTTP endpoints, timeouts, and resilience (`Agentor:TransportResilience`) for Athanor, Conexus, MCP, and external agents. Disabled adapters return deterministic “disabled” detail while **`GET /ready`** may still require auth per matrix — see **`docs/security/AUTHORIZATION_MATRIX.md`**.

## Secret management

- Never commit production connection strings, JWT signing material, or API keys.
- Prefer short-lived tokens and rotation policies for integration credentials.
- Log and diagnostics surfaces apply **redaction**; still treat logs as sensitive (see observability docs).

## Container image

- Build from **`Dockerfile`** at the tagged commit (`docs/RELEASE/v1.0-RC-TAGGING.md`).
- Run as non-root where your orchestrator permits; set **`ASPNETCORE_ENVIRONMENT=Production`**.

## Health and readiness

- **`GET /health`** — anonymous liveness.
- **`GET /ready`** and integration surfaces — require authentication and appropriate permissions; align probes with chosen **Auth** mode (Header probes must send the configured actor header when hitting Agentor directly).

## Verification and release discipline

Complete **`docs/security/SECURITY_RELEASE_CHECKLIST.md`** and **`docs/RELEASE/v1.0-RC-VERIFICATION.md`** for the production promotion window.

## See also

- `docs/operator/runbook.md` — incidents and diagnostics capture.
- `docs/operator/observability.md` — metrics and structured logging.
