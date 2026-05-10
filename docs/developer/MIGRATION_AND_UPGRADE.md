# Migration and upgrade checklist

## Database

1. Backup production database before migration.
2. Apply EF migrations in maintenance window: `dotnet ef database update --project src/Agentor.Infrastructure --startup-project src/Agentor.Api`.
3. Verify schema: `dotnet ef migrations list` matches deployed assemblies.
4. Run application smoke: `/health`, `/ready`, start + complete a fake agent run.

## Rollback

- Restore DB backup taken **before** migration if schema incompatible.
- Deploy previous application image/tag that matches the restored schema.
- Outbox/durable execution: confirm no half-applied migrations; treat ledger/outbox tables per retention policy after rollback.

## Configuration

- Compare `appsettings` sections (`Agentor:*`, integration endpoints). Never deploy new code with stale secrets references.

## Verification

- Full test suite green on release candidate tag.
- Harness harnessPass aligned with phase closeout (see `.agentor-harness/`).