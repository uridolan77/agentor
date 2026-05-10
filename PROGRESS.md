# Agentor Progress

This file is the agent-maintained handoff. Claude Code should read it before doing any work.

## Done

- PR01 runtime foundation completed.
- Documentation overlay applied.
- PR07 persistence work appears implemented.
- CWC-inspired long-running coding harness installed (overlay, hooks, docs, verification scripts).

## In progress

- None.

## Next

- Commit harness as `chore: add Agentor CWC coding harness` (if not already on main).
- Run a fresh-context evaluator pass on that commit before starting PR08.
- PR08 — run read model and query endpoints (with evidence + evaluator per harness).

## Notes

Agentor service boundaries remain:

```text
Agentor executes.
Athanor canonizes.
Conexus routes models.
MCP connects tools later.
External frameworks are adapters, not core.
```

PR07 follow-ups (not blocking harness): prefer append/update persistence over delete/reinsert for audit semantics; add real PostgreSQL integration tests later; strengthen EF policy/tool-call round-trip tests using `StartAgentRunHandler`.
