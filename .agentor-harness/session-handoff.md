# Session handoff — Phase 27 PR118

## Completed

- **EF merge save**: **`EfCoreAgentRunRepository`** loads tracked aggregate with includes; **insert** path uses **`RecordMapper.ToRecord`** once; **update** path patches root JSON columns, **`SyncSteps`**, **`SyncToolCalls`**, **`SyncPolicyDecisions`**, **`AppendTraceEvents`** (canonical JSON compare for trace data).
- **Concurrency**: **`AgentRunRecord.AggregateVersion`** + **`AgentRun.PersistenceConcurrencyVersion`**; stale version → **`AgentRunPersistenceConcurrencyException`**; **`DbUpdateConcurrencyException`** wrapped the same.
- **Immutability**: **`AgentRunTraceImmutabilityException`** when an existing trace id would change payload.
- **Resume cursor**: **`resume_cursor_json`** column; **`GetAsync`** rehydrates **`PlanResumeCursor`** via **`RecordMapper.TryDeserializeResumeCursor`**.
- **HTTP**: **`ExceptionHandlingMiddleware`** — **409** concurrency, **400** trace immutability.
- **Migration / snapshot**: **`20260511200000_Phase27AgentRunPersistence`**; **`AgentorDbContext`** **`AggregateVersion`** concurrency token.
- **Tests / docs**: five new **`EfCoreAgentRunRepositoryTests`**; **`docs/REPO_TRUTH.md`**; planning doc blocker #2 note.

## Verification

- `dotnet restore Agentor.sln` succeeded
- `dotnet build Agentor.sln --no-restore` succeeded
- `dotnet test Agentor.sln --no-build` succeeded (**443 passed, 0 failed**)
- `powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/verify-harness.ps1 -ExpectedPhase 27 -ExpectedHarnessPass PR118` succeeded
- `powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/verify-repo-clean.ps1` succeeded

## What is next

- **Phase 28** — review workflow semantics (per planning doc), or next scheduled phase.

## What was explicitly not started

- **Phase 28+** product work was **not** started.

## Remaining risks / deferred

- **Optional** trace monotonic sequence / extra unique constraints from planning §PR27.4 were **not** added (append-by-id + immutability checks only).
- **No new HTTP integration test** asserts 409/400 bodies for the new middleware branches (evidence is **`ExceptionHandlingMiddleware`** source + **`PR118-008`** harness row).
