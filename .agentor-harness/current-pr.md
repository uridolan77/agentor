# Current PR — harness marker

Completed: Phase 20 PR96-PR100 — Durable operational runtime: IDurableRunQueue + RunQueueRecord + EfRunQueueStore, RunQueueHostedService + RunWorkerOptions (disabled by default, lease-aware processing), OutboxHostedService + OutboxDispatchOptions (disabled by default), atomic EfOutboxStore TryMarkDispatching claim update, and read-only ops endpoints (`GET /api/v1/ops/queue`, `GET /api/v1/ops/outbox`, `GET /api/v1/ops/leases`). Full verification: 357 tests passing; verify-harness and verify-repo-clean passed.

Next: Phase 21 or next explicitly scheduled phase.

Do not start the next phase during closeout.
