# Agentor Builder Protocol

Use this protocol for every medium-long PR pass.

## Start

1. Read:
   - `PROGRESS.md`
   - `AGENTS.md`
   - selected PR spec under `docs/planning/pr1-pr40/prs/`
   - `docs/SERVICE_BOUNDARIES.md`
   - `docs/FRAMEWORK_STRATEGY.md`
2. Run:
   ```powershell
   git status
   git log --oneline -10
   ```
3. Create/update `artifacts/verification/`.

## Work rule

Work on exactly one PR/pass.

Do not implement future PRs early.

## Required evidence

Always capture:

```powershell
dotnet --info *> artifacts/verification/dotnet-info.txt
dotnet restore *> artifacts/verification/dotnet-restore.txt
dotnet build --no-restore *> artifacts/verification/dotnet-build.txt
dotnet test --no-build *> artifacts/verification/dotnet-test.txt
```

Then read those files before marking anything passing.

## Stop rule

Before stopping:

1. Update `PROGRESS.md`.
2. Run verification again if code changed.
3. Produce a final summary:
   - files changed
   - tests run
   - evidence files read
   - boundary risks
   - next step
