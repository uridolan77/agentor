# Current PR — harness marker

Completed: Phase 20 PR96-PR100 + PR100.5 — Durable operational runtime with reconciliation hardening: ops endpoints require `OpsRead` and sanitize error fields; `Service` role is denied `OpsRead`; EF durable queue preserves non-expired claims and allows reclaim of expired claims via load-check-save; run queue completion/failure transitions are worker-ownership checked; outbox hosted dispatch blocks `NoOpOutboxSink` outside Development/Test unless explicit override. EF queue claiming uses atomic `ExecuteUpdateAsync` for pending rows; expired reclaim uses functional load-check-save pattern (not strictly atomic but deterministic). PR100.6 (atomic expired-reclaim via `ExecuteUpdateAsync`) reverted due to SQLite LINQ provider limitations on complex OR + nullable DateTimeOffset predicates. Full verification: restore/build/test (373 tests) all passing; verify-harness and verify-repo-clean passed.

Next: Phase 21 or next explicitly scheduled phase.

Do not start the next phase during closeout.
