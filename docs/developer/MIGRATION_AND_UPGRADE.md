# Migration and upgrade checklist

## EF Core migration inventory (Phase 36 / PR144)

Migrations live under `src/Agentor.Infrastructure/Persistence/Migrations/`. As of Phase 36 closeout, the ordered set is:

| Migration id | Notes |
|----------------|-------|
| `20260510061027_InitialCreate` | Baseline schema |
| `20260510143000_AddAgentRunIdempotencyKeys` | Start-run idempotency |
| `20260510220000_SessionMemoryOnAgentRun` | Session memory JSON |
| `20260511003000_GovernanceScopeAndHumanReview` | Governance scope + HR columns |
| `20260511183000_RunQueueOrchestrationPayload` | Queue orchestration selectors |
| `20260511200000_Phase27AgentRunPersistence` | Trace append semantics, resume cursor, aggregate version |
| `20260511203000_Phase28ReviewWorkflowSemantics` | Review workflow timestamps / status |
| `20260512080000_Phase12Reliability` | Durable queue, outbox, leases |
| `20260512100000_Phase33QueueStructuredToolPayload` | `run_queue_items.tool_payload_json` |

CI runs `dotnet ef migrations list` (see `.github/workflows/ci.yml`) so a missing migration fails the build.

### Provider support boundaries

| Provider | Status |
|----------|--------|
| **PostgreSQL** (`UseNpgsql`) | First-class production path when `Agentor:Persistence:Mode=Postgres` and a connection string are set. |
| **SQLite** | Used heavily in **tests** (`EfCoreAgentRunRepositoryTests`, queue tests, etc.). Not positioned as the supported production HA store. |
| **SQL Server** | No first-class `UseSqlServer` wiring in this repo today; adding it would be a new adapter pass (new provider registration + migration strategy), not implied by existing migrations. |

## Database

1. Backup production database before migration.
2. Apply EF migrations in maintenance window: `dotnet ef database update --project src/Agentor.Infrastructure --startup-project src/Agentor.Api`.
3. Verify schema: `dotnet ef migrations list` matches deployed assemblies.
4. Run application smoke: `/health`, `/ready`, **`scripts/release-smoke.ps1`** (or minimal manual checks), start + complete a fake agent run.

## Rollback

- Restore DB backup taken **before** migration if schema incompatible.
- Deploy previous application image/tag that matches the restored schema.
- Outbox/durable execution: confirm no half-applied migrations; treat ledger/outbox tables per retention policy after rollback.

## Configuration

- Compare `appsettings` sections (`Agentor:*`, integration endpoints). Never deploy new code with stale secrets references.

## Verification

- Full test suite green on release candidate tag.
- Harness harnessPass aligned with phase closeout (see `.agentor-harness/`).
## PR75.6 documentation note

This file is unchanged in substance for PR75.6. RC means **review-ready** boundaries and deferred items are tracked in `docs/RELEASE/v1.0-RC-DEFERRED-ITEMS.md` rather than being silently dropped from the harness.