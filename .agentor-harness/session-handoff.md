# Session handoff — Phase 20 PR96-PR100

## Completed

Phase 20 — Durable operational runtime.

### PR96 — Durable run queue abstraction
- Added `IDurableRunQueue` and `RunQueueRecord` (`DurableRunQueueStatus`) in Application.
- Added `EfRunQueueStore` for EF-backed durable queue persistence.
- Added `InMemoryDurableRunQueueStore` for non-EF mode fallback.
- Wired queue persistence into `InMemoryRunQueue` (enqueue + snapshot + inline processing through durable claims).

### PR97 — Hosted run worker
- Replaced previous worker with `RunQueueHostedService` (in `RunQueueBackgroundWorker.cs` file).
- Added `RunWorkerOptions` (`Agentor:RunWorker`) with `Enabled=false` default, poll interval, lease TTL.
- Hosted worker claims durable queue items and respects `IRunExecutionLeaseStore` contention.

### PR98 — Hosted outbox dispatcher
- Added `OutboxHostedService` and `OutboxDispatchOptions` (`Agentor:OutboxDispatch`) with disabled-by-default behavior.
- Added `NoOpOutboxSink` default sink registration for safe startup.
- Adjusted `OutboxDispatcher` lifetime to scoped.

### PR99 — Atomic outbox claim and contention hardening
- Updated `EfOutboxStore.TryMarkDispatchingAsync` to use atomic conditional `ExecuteUpdateAsync`.
- Added contention simulation test to ensure only one competing dispatcher can transition pending→dispatching.

### PR100 — Operational status endpoints
- Added read-only endpoints in `OpsEndpoints`:
  - `GET /api/v1/ops/queue`
  - `GET /api/v1/ops/outbox`
  - `GET /api/v1/ops/leases`
- Added contracts: `OpsQueueItemDto`, `OpsOutboxItemDto`, `OpsLeaseItemDto`.
- Added API integration test verifying endpoint responses and no-secret output.

### Tests and verification
- New/updated tests:
  - `EfRunQueueStoreTests`
  - `RunQueueHostedServiceTests`
  - `OutboxHostedServiceTests`
  - `Phase12EfRoundTripTests` (outbox contention)
  - `IntegrationEndpointsTests` (ops endpoints)
- Full verification:
  - `dotnet restore Agentor.sln` succeeded
  - `dotnet build Agentor.sln --no-restore` succeeded
  - `dotnet test Agentor.sln --no-build` succeeded (**357 passed, 0 failed**)
  - API smoke evidence: `dotnet test tests/Agentor.Api.Tests/Agentor.Api.Tests.csproj --no-build` (**75 passed, 0 failed**)
  - `verify-harness` succeeded (`ExpectedPhase=20`, `ExpectedHarnessPass=PR100`)
  - `verify-repo-clean` succeeded

## What is next

- Phase 21 or next explicitly scheduled phase.

## What was explicitly not started

- Phase 21+ implementation work.
- SCOPE-001 (policy scope enforcement by Tenant/Workspace/Project) remains deferred.

## Remaining risks / deferred

- `SCOPE-001` remains active and deferred to v1.1.
