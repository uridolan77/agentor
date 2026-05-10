# Agentor Session Closeout Protocol

This file defines what “done” means for Agentor agent sessions.

## Completion definition

A PR, phase, or implementation pass is complete only when:

1. source changes are made,
2. tests pass,
3. harness files are reconciled,
4. acceptance evidence is recorded,
5. handoff is updated,
6. files are UTF-8,
7. the next phase has not been started accidentally.

## Required commands

Run from repository root:

```bash
dotnet restore Agentor.sln
dotnet build Agentor.sln --no-restore
dotnet test Agentor.sln --no-build
```

After harness files are updated, also run (PowerShell):

```powershell
pwsh ./scripts/verify-harness.ps1
pwsh ./scripts/verify-repo-clean.ps1
```

Use `-ExpectedPhase` / `-ExpectedHarnessPass` on `verify-harness.ps1` when closing a numbered phase pass.

Record the exact results and test counts in:

```text
.agentor-harness/verification-log.md
```

## Required harness files

Every closeout must update and then re-read:

```text
.agentor-harness/current-pr.md
.agentor-harness/feature-list.json
.agentor-harness/progress.md
.agentor-harness/verification-log.md
.agentor-harness/session-handoff.md
```

## `current-pr.md` must say

```md
Completed: <actual completed PR/phase>.
Next: <actual next PR/phase>.
```

## `feature-list.json` must include

```json
{
  "phase": 0,
  "harnessPass": "PRx",
  "encoding": "utf-8",
  "acceptanceItems": []
}
```

Acceptance rows must be item-level, not one coarse row per phase.

Good:

```json
{
  "id": "PR55-003",
  "pr": 55,
  "description": "Audit export redacts token-like keys",
  "passes": true,
  "evidence": "GetRunAuditExportQueryHandlerTests.RedactsSensitiveKeys"
}
```

Bad:

```json
{
  "id": "PR55",
  "description": "Audit export done",
  "passes": true
}
```

## Verification-log requirements

Each pass must append:

```md
## <PR/Phase> verification (<date>)

Commands:
- dotnet restore Agentor.sln
- dotnet build Agentor.sln --no-restore
- dotnet test Agentor.sln --no-build

Results:
- restore: OK/FAIL
- build: OK/FAIL
- test: OK/FAIL

Counts:
- Domain:
- Application:
- Infrastructure:
- Api:
- Total:

Scope:
- completed:
- not started:
```

## Session handoff requirements

`session-handoff.md` must include:

```md
# Session handoff

## Completed

## Verification

## Not started

## Remaining risks

## Next recommended step
```

## UTF-8 requirement

All text files must be UTF-8.

The following are not acceptable:

- UTF-16 LE
- UTF-16 BE
- null-byte separated text
- malformed JSON
- invisible encoding corruption

If a file appears with null bytes between characters, rewrite it as UTF-8 before completion.

## Self-audit checklist

Before final response, answer:

1. Does `current-pr.md` point to the correct next PR?
2. Does `feature-list.json` have the correct phase and harnessPass?
3. Are acceptance items item-level?
4. Are false items preserved rather than hidden?
5. Does `verification-log.md` contain the latest test counts?
6. Does `session-handoff.md` say what was not started?
7. Are new files UTF-8?
8. Did the implementation accidentally start the next phase?
9. Did any integration violate the architecture boundary?
10. Did the final response quote the re-read harness state?

If any answer is bad, fix before reporting completion.
