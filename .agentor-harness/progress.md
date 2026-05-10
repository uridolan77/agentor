# Agentor harness progress

## Phase 21 PR105.5 (2026-05-11)

**Status**: Complete.

**Work**:
- Introduced `Agentor.Infrastructure.Http.IntegrationHttpError` for shared non-2xx handling across integration HTTP adapters.
- `HttpRequestException` now carries HTTP **`StatusCode`**; messages include redacted + truncated upstream bodies.
- Documentation and harness updated; Phase 22 not started.

**Verification**:
- `dotnet restore` / `dotnet build --no-restore` / `dotnet test --no-build` on `Agentor.sln` — **400 passed, 0 failed**
- `verify-harness.ps1 -ExpectedPhase 21 -ExpectedHarnessPass PR105.5` (Windows PowerShell)
- `verify-repo-clean.ps1`

**Scope guard**: Phase 22 not started.

## Phase 21 PR101–PR105 (2026-05-11)

**Status**: Complete.

**Work**:
- Athanor, Conexus, MCP, and external-agent HTTP adapters covered with fake-handler contract tests plus policy gating tests for external invoke.
- Conexus request DTO extended with optional declared budget fields; executor passes tool input into gateway; HTTP clients return clearer errors on non-2xx.
- Documentation: `docs/integrations/compatibility-matrix.md` (Fake/Http/Disabled matrix, endpoints, unsupported features).

**Verification**:
- `dotnet restore` / `dotnet build --no-restore` / `dotnet test --no-build` on `Agentor.sln` — **394 passed, 0 failed**
- `verify-harness.ps1 -ExpectedPhase 21 -ExpectedHarnessPass PR105` (via Windows PowerShell)
- `verify-repo-clean.ps1` (via Windows PowerShell)

**Scope guard**: Phase 22 not started.

## Phase 20 PR100.6 — Attempted Atomic Claim Hardening (2026-05-10)

**Status**: Reverted to PR100.5 baseline due to SQLite LINQ translation limitations.

**Work Attempted**:
- Refactored `EfRunQueueStore.TryClaimByIdsAsync` from tracked-entity load-check-save to fully atomic `ExecuteUpdateAsync`
- Complex WHERE predicate: `Status == Pending OR (Status == Claimed AND LeaseExpiresAtUtc <= now)`
- Multiple SetProperty calls in single transaction

**Issue Discovered**:
- SQLite EF Core provider cannot translate complex OR expressions combined with nullable DateTimeOffset comparisons
- Error: `System.InvalidOperationException: The LINQ expression ... could not be translated`
- Affects both: (1) OR + nullable nullable check, (2) multiple SetProperty chainings
- Other EF providers (SQL Server, PostgreSQL) likely support this pattern

**Resolution**: Retained PR100.5 implementation with hybrid approach:
- **Pending claims**: Atomic via simple `ExecuteUpdateAsync` (WHERE: `Status == Pending`)
- **Expired reclaim**: Load-check-save with functional but non-atomic semantics
- Result: 373 tests all passing (72 + 128 + 13 + 71 + 89)

**Verification**:
- `dotnet restore` ✓
- `dotnet build --no-restore` ✓ (0 errors, 0 warnings)
- `dotnet test --no-build` ✓ (373 passed, 0 failed)
- `verify-harness.ps1 -ExpectedPhase 20 -ExpectedHarnessPass PR100.5` ✓
- `verify-repo-clean.ps1` ✓

**Scope Guard**: Phase 21 not started.

**Lessons Learned**: SQLite testing masks database-specific LINQ translation limits not visible until SQL Server deployment. Complex predicates should be split into separate queries per EF provider compatibility.

## Phase 20 PR100.5 (2026-05-10)

Completed **Phase 20 reconciliation, ops security, and durability hardening**:

- Added `OpsRead` authorization permission and enforced it on all ops endpoints:
	- `GET /api/v1/ops/queue`
	- `GET /api/v1/ops/outbox`
	- `GET /api/v1/ops/leases`
- Hardened default role mapping in `RoleBasedAuthorizationDecisionService`:
	- `Service` remains read-only but is explicitly denied `OpsRead`.
- Added ops output sanitization in `OpsEndpoints`:
	- queue/outbox error text is redacted for common secret-bearing tokens and truncated for safe operator display.
- Strengthened durable queue claim behavior in `EfRunQueueStore`:
	- claim loop now reclaims `Claimed` rows only when lease is expired.
	- non-expired claimed rows are not stealable.
- Strengthened durable completion ownership semantics:
	- `IDurableRunQueue.MarkCompletedAsync` and `MarkFailedAsync` now require `workerId`.
	- EF and in-memory durable queue stores now enforce worker ownership checks.
	- run queue worker wiring updated to pass worker identity for completion/failure transitions.
- Hardened outbox sink safety:
	- added `OutboxDispatchOptions.AllowNoOpSinkOutsideDevelopment` (default false).
	- `OutboxHostedService` now throws when dispatch is enabled with `NoOpOutboxSink` outside Development/Test unless explicit override is set.
- Updated docs:
	- `docs/security/auth-boundary.md` (OpsRead permission + ops endpoint authorization)
	- `docs/security/deployment-threat-notes.md` (ops/read exposure + no-op outbox sink guard)
	- `docs/planning/pr76-125/Phase 20 — Durable operational runtime.md` (PR100.5 reconciliation acceptance)

Added/updated tests:

- `RoleBasedAuthorizationDecisionServiceTests` (OpsRead allowed/denied by role)
- `EndpointAuthorizationApiTests` (ops endpoints allow/forbid/unauthorized)
- `IntegrationEndpointsTests` (ops response secret redaction/truncation regression)
- `EfRunQueueStoreTests` (expired claim reclaim, active claim protection, ownership transitions)
- `OutboxHostedServiceTests` (prod guard for no-op sink + explicit override)

Verification:

- `dotnet restore Agentor.sln` succeeded
- `dotnet build Agentor.sln --no-restore` succeeded
- `dotnet test Agentor.sln --no-build` succeeded
- `pwsh -NoProfile -ExecutionPolicy Bypass -File ./scripts/verify-harness.ps1 -ExpectedPhase 20 -ExpectedHarnessPass PR100.5` succeeded
- `pwsh -NoProfile -ExecutionPolicy Bypass -File ./scripts/verify-repo-clean.ps1` succeeded

Active deferred items (`passes: false`): `SCOPE-001` only.

Scope guard: Phase 21 was not started in this pass.

## Phase 19 PR95.5 (2026-05-10)

Completed **Phase 19 authorization hardening**:

- Protected alias endpoints with explicit authorization checks in `Phase13ProductEndpoints`:
	- `GET /runs/{runId}/audit-packet` requires `AuditRead`
	- `POST /reviews/{runId}/decisions` requires `GovernanceReviewWrite`
- Added authorization to review inbox endpoint:
	- `GET /reviews/pending` requires `GovernanceReviewRead`
- Hardened Jwt role handling in `HeaderOrFakeActorAccessor`:
	- missing role claim -> actor resolution failure
	- unrecognized role claim -> actor resolution failure
	- no fallback to `HumanOperator`
- Added/updated tests:
	- alias endpoint auth coverage in `EndpointAuthorizationApiTests`
	- strict Jwt role tests in `HeaderOrFakeActorAccessorTests`
	- read permission role test in `RoleBasedAuthorizationDecisionServiceTests`
- Updated security docs with explicit Jwt principal-consumption caveat and alias-bypass prevention notes.

Active deferred items (`passes: false`): `SCOPE-001` only.

Scope guard: no Phase 20 work started in this pass.

## Phase 20 PR96-PR100 (2026-05-10)

Completed **Phase 20 durable operational runtime**:

- **PR96**: Added durable queue abstraction and records: `IDurableRunQueue`, `RunQueueRecord`, `DurableRunQueueStatus`; implemented `EfRunQueueStore` and `InMemoryDurableRunQueueStore`.
- **PR97**: Added hosted run worker `RunQueueHostedService` with `RunWorkerOptions` (`Agentor:RunWorker`) and lease-aware processing via `IRunExecutionLeaseStore`.
- **PR98**: Added hosted outbox dispatcher `OutboxHostedService` with `OutboxDispatchOptions` (`Agentor:OutboxDispatch`) and default `NoOpOutboxSink`.
- **PR99**: Tightened EF outbox dispatch claiming with atomic conditional update (`ExecuteUpdateAsync` pending→dispatching) and contention coverage.
- **PR100**: Added read-only operational endpoints:
	- `GET /api/v1/ops/queue`
	- `GET /api/v1/ops/outbox`
	- `GET /api/v1/ops/leases`

Added tests:

- `EfRunQueueStoreTests` (enqueue persistence, claim, restart durability)
- `RunQueueHostedServiceTests` (disabled by default, enabled processing, lease contention)
- `OutboxHostedServiceTests` (disabled by default, enabled dispatch, retry/poison)
- `Phase12EfRoundTripTests` contention case for atomic outbox claim
- `IntegrationEndpointsTests` ops endpoint coverage and no-secrets assertion

Verification:

- `dotnet restore Agentor.sln` succeeded
- `dotnet build Agentor.sln --no-restore` succeeded
- `dotnet test Agentor.sln --no-build` succeeded (**357 passed, 0 failed**)
- `dotnet test tests/Agentor.Api.Tests/Agentor.Api.Tests.csproj --no-build` succeeded (**75 passed, 0 failed**) captured as API smoke evidence
- `verify-harness` passed (`ExpectedPhase=20`, `ExpectedHarnessPass=PR100`)
- `verify-repo-clean` passed

Active deferred items (`passes: false`): `SCOPE-001` only.

## Phase 19 PR91-PR95 (2026-05-10)

Completed **Phase 19 production identity and authorization boundary**:

- Added `Agentor:Auth` options (`Fake|Header|Jwt`) with startup validation via `AgentorAuthOptionsValidator`.
- Enforced safe default posture: Fake mode blocked outside Development/Test unless explicitly overridden.
- Extended JWT actor accessor behavior with configurable claim mappings for actor id, display name, and role.
- Added authorization primitives (`AgentorPermission`, `AuthorizationDecision`, `IAuthorizationDecisionService`).
- Added `RoleBasedAuthorizationDecisionService` default mapping (`Service` read-only; governance/policy writes require `HumanOperator`/`System`).
- Added `EndpointAuthorization.Require(...)` and applied permission checks to governance + policy endpoints:
	- `POST /agent-runs/{runId}/human-review` -> `GovernanceReviewWrite`
	- `GET /agent-runs/{runId}/audit-export` -> `AuditRead`
	- `GET /policy-bundles` and `GET /policy-bundles/{id}` -> `PolicyBundleRead`
	- `POST /policy-bundles` and `POST /policy-profiles/{id}/activate` -> `PolicyBundleWrite`
- Added API and unit tests for auth mode behavior, configurable JWT claims, role-based allow/deny, and endpoint authorization integration.
- Added docs: `docs/security/auth-boundary.md`, `docs/security/deployment-threat-notes.md`.
- Updated `docs/GOVERNANCE_BOUNDARY.md` and `docs/RELEASE/v1.0-RC-DEFERRED-ITEMS.md` (SCOPE-001 seam note retained, still deferred).

Active deferred items (`passes: false`): `SCOPE-001` only.

Test totals after Phase 19 verification: **346 passing, 0 failing** across all 5 test projects.
Verification scripts: `verify-harness` passed, `verify-repo-clean` passed.

## Phase 18 PR90.5 (2026-05-10)

Completed **Phase 18 hardening and closeout correction**:

- Deleted stale harness debris: `.agentor-harness/feature-list.json.head.txt`.
- Tightened `scripts/verify-repo-clean.ps1` to scan `.txt` files under `.agentor-harness` for encoding issues and to fail explicitly on stale harness snapshot files such as `feature-list.json.head.txt`.
- Corrected `docs/RELEASE/v1.0-RC-DEFERRED-ITEMS.md`: `PR53-005` removed from deferred items; `SCOPE-001` is now the only active deferred item.
- Hardened resumed execution `EscalateToReview` semantics in `ApplyHumanReviewDecisionHandler.ExecutePendingResumeStepAsync`: denied/failed resumed tools now remain reviewable as `ToolCallStatus.RequiresReview`, so `ApplyHumanReviewDecision()` can act on the state safely.
- Added focused tests for resumed-step escalation after policy deny and after tool failure in `MultiStepReviewResumeTests.cs`.
- Updated `docs/design/multi-step-review-resume.md` to define the reviewable-tool contract for resumed-step `EscalateToReview` and to note non-resume parity as remaining technical debt.

Active deferred items (`passes: false`): `SCOPE-001` only.

Test totals after PR90.5 verification: **333 passing, 0 failing** across all 5 test projects. Focused hardening slice: `MultiStepReviewResumeTests` 12 passed, 0 failed.

## Phase 18 PR86–PR90 (2026-05-10)

Completed **Phase 18 Multi-step Human Review Resume Semantics**:

- **PR86**: Design doc `docs/design/multi-step-review-resume.md` — ReviewCheckpoint, ResumeCursor, PendingPlanStep, ReviewedToolContinuation; FailFast/ContinueOnFailure/SkipRemaining/EscalateToReview interactions; invariants (approval never overrides Deny; approval does not canonize knowledge; chaining semantics).
- **PR87**: `PlanResumeCursor`, `PendingPlanStep`, `PlanStepResumeSnapshot`, `ReviewResumeState` in `Agentor.Domain.Governance.ReviewResumeCursor.cs`. `AgentRun.ResumeCursor` property + `RecordPlanResumeCursor` / `ClearResumeCursor` methods. `TraceEventKind.PlanResumeCursorRecorded/Cleared/MultiStepPlanResumed`. `Reconstitute` accepts `PlanResumeCursor?`. 13 domain tests in `PlanResumeCursorTests.cs`.
- **PR88**: `SequentialAgentPlanExecutor.RecordResumeCursorIfNeeded` — records cursor with remaining-step list and completed-step history when `RequiresReview` occurs mid-plan. `ApplyHumanReviewDecisionHandler.ResumeRemainingPlanStepsAsync` + `ExecutePendingResumeStepAsync` — resumes remaining steps with full policy evaluation, failure-policy handling (ContinueOnFailure, SkipRemaining, MarkForCompensation, EscalateToReview, FailFast), and RequiresReview chaining (new cursor recorded on re-suspension). `RecordNewCursorForResumedStep` for multi-gate plans. PR90.5 adds 2 focused escalation-hardening tests, bringing `MultiStepReviewResumeTests.cs` to 12 tests.
- **PR89**: `GovernanceResumeApiTests.cs` — 6 API integration tests covering Approve (multistep completion), Reject (failure), RequestChanges (unchanged), Escalate (unchanged), 409 on non-RequiresReview run, 404 on unknown run. `GovernanceResumeApiFixture` using `TestAgentRunRepository`.
- **PR90**: `review-gated-multistep-plan.json` (schema 5, kind MultiStepReviewResumeEvaluation), `review-resume-audit-export.json` (schema 5, kind ReviewResumeAuditExport). `registry.json` updated to 4 entries. 3 tests in `Phase18FixtureTests.cs` including the named PR53-005 evidence test.

**PR53-005 closed**: Multi-step plan executor resume semantics with named evidence.
Active deferred items (`passes: false`): `SCOPE-001` (policy rule scope enforcement, v1.1).

Test totals: **331 passing, 0 failing** across all 5 test projects.
Evidence: `artifacts/verification/dotnet-{info,restore,build,test}.txt`.

## Phase 17 PR85.5 (2026-05-10)

Policy deferred-item reconciliation after Phase 17.

- **PR52-004 closed**: The versioned `PolicyBundle` enterprise policy model is fully implemented by Phase 17. Row flipped to `passes: true` in `feature-list.json`. Engineering note added to `v1.0-RC-DEFERRED-ITEMS.md`.
- **SCOPE-001 documented**: `PolicyRuleScope` (Global/Tenant/Workspace/Project) is modeled on `PolicyRule` but `PolicyBundleRulesAdapter.ToProfileRules()` does not filter by run identity — all rules are treated as globally effective. Explicit `SCOPE-001` comment added to adapter. "Known limitations" section added to `docs/developer/policy-bundles.md`. New deferred item `SCOPE-001` added to `v1.0-RC-DEFERRED-ITEMS.md` and `feature-list.json` (`passes: false`).
- **Phase 18 not started.**

Active deferred items (`passes: false`): `SCOPE-001`, `PR53-005`.

## Phase 17 PR81–PR85 (2026-05-10)

Completed **Phase 17 Enterprise Policy Model**:

- **PR81**: `PolicyBundle`, `PolicyBundleVersion`, `PolicyRule`, `PolicyRuleKind/Scope/Effect` domain model in `Agentor.Domain.Policy`. Versioned and immutable after publication. Duplicate rule IDs rejected.
- **PR82**: `PolicyProfile`, `PolicyProfileBinding`, `ActivePolicyProfile` domain types. `IPolicyBundleRepository` and `IPolicyProfileRepository` application abstractions.
- **PR83**: `PolicyBundleRulesAdapter` (bundle → `PolicyProfileRules`). `InMemoryPolicyBundleRepository`, `InMemoryPolicyProfileRepository`. `RuntimePolicyEvaluator` extended with bundle-aware 2-constructor pattern (3-param test constructor + 5-param DI constructor). `PolicyProfileRules.RequiresReviewToolKeys` added. `RequiresReview` remains distinct from `Deny`.
- **PR84**: `PolicyBundleDtos.cs` contracts. `PolicyBundleEndpoints.cs` (GET/POST bundles, POST activate). `Program.cs` registration. DI wiring in `DependencyInjection.cs`.
- **PR85**: `PolicyBundleTests.cs` (Domain.Tests — 25 new tests). `PolicyBundleEvaluationTests.cs` (Application.Tests — 13 new tests). Fixture JSONs: `allow-bundle.json`, `deny-bundle.json`, `review-bundle.json`. Audit export updated with `policyIdentity` section. `docs/developer/policy-bundles.md`.

Test totals: **298 passing, 0 failing** across all 5 test projects.

Evidence: `artifacts/verification/dotnet-{info,restore,build,test}.txt`.

## Phase 15 + PR75.8 (2026-05-10)

Completed **PR75.8** after **PR75.7**: closes Athanor API acceptance gaps **PR23-API-003** and **PR24-API-003** using `WebApplicationFactory` + `TestAgentRunRepository` to seed an `AgentRun` in **Running** (default POST `/api/v1/agent-runs` completes synchronously, so it cannot leave a running run).

- New tests: `tests/Agentor.Api.Tests/AthanorRunningRunApiTests.cs` (204 No Content on evidence-provenance, 202 Accepted on candidates).
- Support types: `tests/Agentor.Api.Tests/Support/TestAgentRunRepository.cs`, `AthanorRunningRunApiFixture.cs`.

**Not started:** Phase 16+ roadmap / v1.1 PolicyBundle / multi-step review resume (still tracked as false in harness where applicable).

Next harness marker: post–Phase 15 work when scheduled; do not start the next phase during closeout.
