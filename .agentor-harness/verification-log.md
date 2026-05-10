# Verification log

## Phase 19 PR95.5 (2026-05-10)

```powershell
dotnet restore Agentor.sln
dotnet build Agentor.sln --no-restore
dotnet test Agentor.sln --no-build
pwsh -NoProfile -ExecutionPolicy Bypass -File ./scripts/verify-harness.ps1 -ExpectedPhase 19 -ExpectedHarnessPass PR95.5
pwsh -NoProfile -ExecutionPolicy Bypass -File ./scripts/verify-repo-clean.ps1
```

Results:

- Restore: succeeded
- Build: succeeded
- Tests: **364 passed, 0 failed**
- verify-harness: passed (`ExpectedPhase=19`, `ExpectedHarnessPass=PR95.5`)
- verify-repo-clean: passed

Notes:

- PR95.5 focused on alias endpoint authorization parity + strict Jwt role handling.
- API test project after hardening: **82 passed, 0 failed**.

## Phase 20 PR96-PR100 (2026-05-10)

```powershell
dotnet --info | Tee-Object artifacts/verification/dotnet-info.txt
dotnet restore Agentor.sln | Tee-Object artifacts/verification/dotnet-restore.txt
dotnet build Agentor.sln --no-restore | Tee-Object artifacts/verification/dotnet-build.txt
dotnet test Agentor.sln --no-build | Tee-Object artifacts/verification/dotnet-test.txt
dotnet test tests/Agentor.Api.Tests/Agentor.Api.Tests.csproj --no-build | Tee-Object artifacts/verification/api-smoke.txt
pwsh -NoProfile -ExecutionPolicy Bypass -File ./scripts/verify-harness.ps1 -ExpectedPhase 20 -ExpectedHarnessPass PR100
pwsh -NoProfile -ExecutionPolicy Bypass -File ./scripts/verify-repo-clean.ps1
```

Results:

- dotnet info: succeeded
- Restore: succeeded
- Build: succeeded (0 warnings, 0 errors)
- Tests: **357 passed, 0 failed**
- API smoke evidence: **75 passed, 0 failed**
- verify-harness: passed (`ExpectedPhase=20`, `ExpectedHarnessPass=PR100`)
- verify-repo-clean: passed

Counts:

- Agentor.Domain.Tests: Passed 72 / Total 72
- Agentor.Contracts.Tests: Passed 13 / Total 13
- Agentor.Application.Tests: Passed 128 / Total 128
- Agentor.Infrastructure.Tests: Passed 69 / Total 69
- Agentor.Api.Tests: Passed 75 / Total 75

Scope:

- completed: PR96-PR100 durable operational runtime (durable queue + hosted worker + hosted outbox + atomic outbox claim + ops endpoints)
- not started: Phase 21+

## Phase 19 PR91-PR95 (2026-05-10)

```powershell
dotnet restore Agentor.sln
dotnet build Agentor.sln --no-restore
dotnet test Agentor.sln --no-build
pwsh -NoProfile -ExecutionPolicy Bypass -File ./scripts/verify-harness.ps1
pwsh -NoProfile -ExecutionPolicy Bypass -File ./scripts/verify-repo-clean.ps1
```

Results:

- Restore: succeeded
- Build: succeeded
- Tests: **346 passed, 0 failed**
- verify-harness: passed
- verify-repo-clean: passed

Notes:

- Phase 19 added auth mode + authorization boundary behavior and endpoint permission enforcement.
- API test project after new coverage: **74 passed, 0 failed**.

## Phase 18 PR90.5 (2026-05-10)

```powershell
dotnet restore Agentor.sln
dotnet build Agentor.sln --no-restore
dotnet test Agentor.sln --no-build
pwsh -NoProfile -ExecutionPolicy Bypass -File ./scripts/verify-harness.ps1 -ExpectedPhase 18 -ExpectedHarnessPass PR90.5
pwsh -NoProfile -ExecutionPolicy Bypass -File ./scripts/verify-repo-clean.ps1
```

Results:

- Restore: succeeded
- Build: succeeded
- Tests: **333 passed, 0 failed**
- verify-harness: passed (ExpectedPhase=18, ExpectedHarnessPass=PR90.5)
- verify-repo-clean: passed

Notes:

- PR90.5 changed only Phase 18 hardening/harness hygiene scope.
- Focused validation before the full run: `MultiStepReviewResumeTests` **12 passed, 0 failed**.

## Phase 18 PR90 (2026-05-10)

```bash
dotnet restore Agentor.sln
dotnet build Agentor.sln --no-restore
dotnet test Agentor.sln --no-build
```

Results:

- Restore: succeeded
- Build: succeeded — 0 warnings, 0 errors
- Tests: **331 passed, 0 failed** (+33 new tests vs Phase 17: 13 domain cursor tests, 9 executor/handler resume tests, 3 fixture tests, 6 API integration tests, 1 eval registry update, 1 eval fixture test)
- New test files: `PlanResumeCursorTests.cs`, `MultiStepReviewResumeTests.cs`, `Phase18FixtureTests.cs`, `GovernanceResumeApiTests.cs`
- Updated test: `EvaluationFixtureRegistryTests` (expected entry count 2→4)
- Evidence artifacts: `artifacts/verification/dotnet-{info,restore,build,test}.txt`
- `verify-harness.ps1` and `verify-repo-clean.ps1` could not execute (PowerShell execution policy blocks unsigned scripts in this environment). All harness conditions verified manually: current-pr.md has Completed/Next, feature-list.json is valid JSON with phase=18/harnessPass=PR90/all passes:true items have evidence, verification-log.md has all required commands, session-handoff.md has not-started statement.

## Phase 17 PR85.5 (2026-05-10)

```bash
dotnet restore Agentor.sln
dotnet build Agentor.sln --no-restore
dotnet test Agentor.sln --no-build
pwsh ./scripts/verify-harness.ps1 -ExpectedPhase 17 -ExpectedHarnessPass PR85.5
pwsh ./scripts/verify-repo-clean.ps1
```

Results:

- Restore: succeeded
- Build: succeeded — 0 warnings, 0 errors
- Tests: 298 passed, 0 failed (unchanged from PR85 — no new test code in PR85.5)
- verify-harness: passed (ExpectedPhase=17, ExpectedHarnessPass=PR85.5)
- verify-repo-clean: passed

---

## Phase 17 PR81–PR85 (2026-05-10)

```bash
dotnet restore Agentor.sln
dotnet build Agentor.sln --no-restore
dotnet test Agentor.sln --no-build
```

Results:

- Restore: succeeded
- Build: succeeded — 0 warnings, 0 errors
- Tests:
  - Agentor.Domain.Tests:       Passed  58 / Total  58 (+20 new policy domain tests)
  - Agentor.Application.Tests:  Passed 113 / Total 113 (+13 new bundle evaluation tests)
  - Agentor.Contracts.Tests:    Passed  13 / Total  13
  - Agentor.Infrastructure.Tests: Passed 59 / Total 59
  - Agentor.Api.Tests:          Passed  55 / Total  55
  - **Grand total: 298 passed, 0 failed, 0 skipped**

Evidence files: `artifacts/verification/dotnet-{info,restore,build,test}.txt`

---

## Phase 15 PR71–PR75 + PR75.5–PR75.8 (legacy)

```powershell
dotnet restore Agentor.sln
dotnet build Agentor.sln --no-restore
dotnet test Agentor.sln --no-build
```

## Results

- Restore: succeeded
- Build: succeeded (Debug); benchmark project included in solution build
- Test assemblies:
  - Agentor.Domain.Tests: Passed 38 / Total 38
  - Agentor.Contracts.Tests: Passed 3 / Total 3
  - Agentor.Application.Tests: Passed 92 / Total 92
  - Agentor.Infrastructure.Tests: Passed 59 / Total 59
  - Agentor.Api.Tests: Passed 53 / Total 53
- **Grand total: 245 tests passed**

## Harness verification

```powershell
pwsh ./scripts/verify-harness.ps1 -ExpectedPhase 15 -ExpectedHarnessPass PR75.5
```

If `pwsh` is not on PATH, use Windows PowerShell with the same arguments:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/verify-harness.ps1 -ExpectedPhase 15 -ExpectedHarnessPass PR75.5
```

**Result:** Harness verification passed (this log entry used the `powershell -File` form above).

## Phase 15 extras

- Benchmark project: `benchmarks/Agentor.Benchmarks` builds with solution.
- CI workflow updated (docker build, dotnet-ef migrations list, evaluation slice, trx artifact).

## PR75.6 verification (2026-05-10)

Commands:

- dotnet restore Agentor.sln
- dotnet build Agentor.sln --no-restore
- dotnet test Agentor.sln --no-build
- powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/verify-harness.ps1 -ExpectedPhase 15 -ExpectedHarnessPass PR75.6
- powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/verify-repo-clean.ps1

Results:

- restore: succeeded
- build: succeeded
- test: succeeded
- verify-harness: passed
- verify-repo-clean: passed

Counts:

- Agentor.Domain.Tests: Passed 38 / Total 38
- Agentor.Application.Tests: Passed 97 / Total 97
- Agentor.Contracts.Tests: Passed 13 / Total 13
- Agentor.Infrastructure.Tests: Passed 59 / Total 59
- Agentor.Api.Tests: Passed 53 / Total 53
- **Grand total: 260 tests passed**

Scope:

- completed: PR75.6 repository hygiene (delete root scratch Python, repo verifier, harness/doc tightening, tests)
- not started: Phase 16+ roadmap / new product features

## PR75.7 verification (2026-05-10)

Commands:

```powershell
dotnet restore Agentor.sln
dotnet build Agentor.sln --no-restore
dotnet test Agentor.sln --no-build
powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/verify-harness.ps1 -ExpectedPhase 15 -ExpectedHarnessPass PR75.7
powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/verify-repo-clean.ps1
```

Results:

- restore: succeeded
- build: succeeded
- test: succeeded
- verify-harness: passed
- verify-repo-clean: passed

Counts:

- Agentor.Domain.Tests: Passed 38 / Total 38
- Agentor.Application.Tests: Passed 97 / Total 97
- Agentor.Contracts.Tests: Passed 13 / Total 13
- Agentor.Infrastructure.Tests: Passed 59 / Total 59
- Agentor.Api.Tests: Passed 53 / Total 53
- **Grand total: 260 tests passed**

Scope:

- completed: PR75.7 repository tightening (broad verify-repo-clean, CI harness checks, AGENTS.md, Program.cs endpoint modules, harness notes, deferred-items + repo status doc, UTF-8 no BOM)
- not started: Phase 16+ roadmap / new product features

## PR75.8 verification (2026-05-10)

Commands:

```powershell
dotnet restore Agentor.sln
dotnet build Agentor.sln --no-restore
dotnet test Agentor.sln --no-build
powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/verify-harness.ps1 -ExpectedPhase 15 -ExpectedHarnessPass PR75.8
powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/verify-repo-clean.ps1
```

Results:

- restore: succeeded
- build: succeeded
- test: succeeded
- verify-harness: passed
- verify-repo-clean: passed

Counts:

- Agentor.Domain.Tests: Passed 38 / Total 38
- Agentor.Application.Tests: Passed 97 / Total 97
- Agentor.Contracts.Tests: Passed 13 / Total 13
- Agentor.Infrastructure.Tests: Passed 59 / Total 59
- Agentor.Api.Tests: Passed 55 / Total 55
- **Grand total: 262 tests passed**

Scope:

- completed: PR75.8 Athanor running-run API tests (PR23-API-003, PR24-API-003); TestAgentRunRepository + AthanorRunningRunApiFixture; harness + deferred-items update
- not started: Phase 16+ roadmap; v1.1 PolicyBundle (PR52-004) and multi-step resume (PR53-005) remain out of scope
