# Session handoff — Phase 20 PR100.5

## Completed

PR100.5 — Phase 20 reconciliation, ops security, and durability hardening.

### Ops authorization and surface hardening
- Added `OpsRead` permission and enforced it for:
  - `GET /api/v1/ops/queue`
  - `GET /api/v1/ops/outbox`
  - `GET /api/v1/ops/leases`
- Updated role policy so `Service` is explicitly denied `OpsRead` (while retaining existing read-only governance permissions).
- Added ops output sanitization for queue/outbox error fields to reduce secret leakage risk and bound payload size.

### Durable queue hardening
- `IDurableRunQueue.MarkCompletedAsync` and `MarkFailedAsync` now require `workerId`.
- EF and in-memory durable queue stores enforce claim ownership on complete/fail transitions.
- EF queue claim path reclaims expired claimed rows and does not steal non-expired claims.

### Outbox dispatch safety hardening
- Added `OutboxDispatchOptions.AllowNoOpSinkOutsideDevelopment` (default false).
- `OutboxHostedService` now throws if dispatch is enabled with `NoOpOutboxSink` outside Development/Test unless explicit override is set.

### Documentation updates
- Updated `docs/security/auth-boundary.md` for `OpsRead`, role mapping, and ops endpoint authorization.
- Updated `docs/security/deployment-threat-notes.md` for ops/read exposure and no-op sink guard controls.
- Updated `docs/planning/pr76-125/Phase 20 — Durable operational runtime.md` with PR100.5 acceptance criteria.

### Tests
- Added/updated API tests for `OpsRead` allow/forbid/unauthorized paths.
- Added ops sanitization regression assertions.
- Added EF queue tests for expired claim reclaim/non-steal and ownership checks.
- Added outbox hosted service tests for no-op sink guard behavior and explicit override path.

## Verification

- `dotnet --info` succeeded
- `dotnet restore Agentor.sln` succeeded
- `dotnet build Agentor.sln --no-restore` succeeded
- `dotnet test Agentor.sln --no-build` succeeded (**377 passed, 0 failed**)
- `dotnet test tests/Agentor.Api.Tests/Agentor.Api.Tests.csproj --no-build` succeeded (**89 passed, 0 failed**)
- `pwsh -NoProfile -ExecutionPolicy Bypass -File ./scripts/verify-harness.ps1 -ExpectedPhase 20 -ExpectedHarnessPass PR100.5` succeeded
- `pwsh -NoProfile -ExecutionPolicy Bypass -File ./scripts/verify-repo-clean.ps1` succeeded

## What is next

- Phase 21 or next explicitly scheduled phase.

## What was explicitly not started

- Phase 21 implementation work was not started.
- SCOPE-001 policy scope enforcement was not started (remains deferred).
- Any unrelated post-Phase-20 feature work was not started.

## Remaining risks / deferred

- `SCOPE-001` remains active and deferred to v1.1.
