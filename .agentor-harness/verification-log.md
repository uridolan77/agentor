# Verification log

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
