# Agentor Progress

This file is the agent-maintained handoff. Claude Code should read it before doing any work.

## Done

- PR01 runtime foundation completed.
- Documentation overlay applied.
- PR07 persistence work appears implemented.
- CWC-inspired long-running coding harness installed (overlay, hooks, docs, verification scripts).
- PR08 run read model and query endpoints: list runs, trace/steps/tool-calls sub-resources, repository list paging, API tests, evidence captured.
- PR08 fresh-context evaluator: PASS on `a2c83d6` (read-model scope, boundaries, build/test + api-smoke evidence).

## In progress

- None.

## Next

- PR09 — idempotency / command deduplication (per roadmap), or PR10 integration tests when scheduled.

## Notes

Agentor service boundaries remain:

```text
Agentor executes.
Athanor canonizes.
Conexus routes models.
MCP connects tools later.
External frameworks are adapters, not core.
```

PR07 follow-ups (not blocking read APIs): prefer append/update persistence over delete/reinsert for audit semantics; add real PostgreSQL integration tests later; strengthen EF policy/tool-call round-trip tests using `StartAgentRunHandler`.
