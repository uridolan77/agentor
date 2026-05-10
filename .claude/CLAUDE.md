# Agentor Claude Code Long-Running Conventions

## Always start here

Before doing anything else:

1. Read `PROGRESS.md`.
2. Run `git status`.
3. Run `git log --oneline -10`.
4. Read the selected PR spec.
5. Read `AGENTS.md` and `docs/SERVICE_BOUNDARIES.md`.

If `PROGRESS.md` does not exist, create it with:

```text
## Done
## In progress
## Next
## Notes
```

## One PR/pass at a time

Work on exactly one selected PR/pass.

If the user gives a new task mid-session, add it to `PROGRESS.md`, then finish the current approved task unless explicitly told to stop.

## Proof before passing

Do not claim a PR is done until you have captured and read verification evidence.

For Agentor, evidence goes under:

```text
artifacts/verification/
```

Minimum evidence:

```text
dotnet-info.txt
dotnet-restore.txt
dotnet-build.txt
dotnet-test.txt
```

API PRs should also include:

```text
api-smoke.txt
```

Persistence PRs should include:

```text
postgres-smoke.txt
```

## Keep PROGRESS.md current

Update `PROGRESS.md` after each coherent checkpoint.

## Boundaries

Never introduce the following unless the current PR explicitly says so:

- Athanor runtime dependency
- Conexus runtime dependency
- MCP runtime dependency
- Semantic Kernel
- Microsoft Agent Framework
- A2A
- direct model provider calls
- dashboard
- unrelated future PR scope

## Operator steering

If an `OPERATOR STEERING:` message appears, treat it as higher priority than your current plan.
