# Current PR — harness marker

Completed: Phase 27 **PR118** — **EF aggregate persistence correctness**: **`EfCoreAgentRunRepository.SaveAsync`** **merge** into tracked **`agent_runs`** (no delete/reinsert); upsert **steps** / **tool_calls** / **policy_decisions**; **append-only** **`trace_events`** by id with **`AgentRunTraceImmutabilityException`** on rewrite; **`resume_cursor_json`** + **`RecordMapper`** round-trip **`PlanResumeCursor`**; **`aggregate_version`** optimistic concurrency + domain **`AgentRun.PersistenceConcurrencyVersion`**; **`AgentRunPersistenceConcurrencyException`** / **`AgentRunTraceImmutabilityException`** in **`Agentor.Application.Abstractions`**; **`ExceptionHandlingMiddleware`** **409** / **400**; migration **`20260511200000_Phase27AgentRunPersistence`** + **`AgentorDbContextModelSnapshot`**; tests **`EfCoreAgentRunRepositoryTests`** (trace dedup, immutability, resume cursor, HR order, SQLite stale concurrency). Docs **`docs/REPO_TRUTH.md`** persistence bullets; planning intro **`docs/planning/pr76-125/Phase 23 - 31.md`** blocker #2 updated. Verification: restore/build/test — **443 passed**; harness scripts **ExpectedPhase 27 / PR118**.

Next: Phase 28 (review workflow semantics) or next explicitly scheduled phase.

Do not start the next phase during closeout.
