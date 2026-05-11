# Current PR — harness marker

Completed: Phase 36 **PR143–PR148** (RC consolidation) **+ PR148.5** (RC closeout polish): **`IntegrationSmokeCommandLine`** rejects unknown flags and missing `--target` / `--output` values (CLI exit **2**); **`tools/Agentor.IntegrationSmoke/Program.cs`** delegates to the testable parser; **`IntegrationSmokeCommandLineTests`** (9 cases); **`scripts/release-smoke.ps1`** optional **`-OutputDirectory`** writes **`release-smoke-report.json`** / **`.md`**; **`docs/operator/release-smoke.md`** report section; **`docs/developer/MIGRATION_AND_UPGRADE.md`** drops the stale PR75.6 note while keeping migration inventory + provider boundaries. Harness: **`phase` 36**, **`harnessPass` PR148.5**; CI **`verify-harness`** ExpectedPhase **36** / **PR148.5**. Verification: restore/build/test on **`Agentor.sln`** — **539 passed**; **`verify-harness`** ExpectedPhase **36** / **PR148.5**; **`verify-repo-clean`**.

Next: Phase 37 (observability and operator readiness) only when explicitly scheduled.

Do not start the next phase during closeout.
