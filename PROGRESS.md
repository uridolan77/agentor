# Agentor Progress

This file is the agent-maintained handoff. Claude Code should read it before doing any work.

## Done

- PR01 runtime foundation completed.
- Documentation overlay applied.
- PR07 persistence work appears implemented.
- CWC-inspired long-running coding harness installed (overlay, hooks, docs, verification scripts).
- PR08 run read model and query endpoints: list runs, trace/steps/tool-calls sub-resources, repository list paging, API tests, evidence captured.
- PR08 fresh-context evaluator: PASS on `a2c83d6` (read-model scope, boundaries, build/test + api-smoke evidence).
- **PR09** — POST `/api/v1/agent-runs` idempotency via `Idempotency-Key`, request fingerprint, in-memory + EF ledger, replay vs conflict, explicit no-key behavior tested.
- **PR10** — Repository contract tests (InMemory + EF InMemory), API integration baseline test, eval fixture JSON + test, EF round-trip strengthened via `StartAgentRunHandler`.
- **PR11** — `ToolDefinition`, `ToolRiskLevel`, `IToolRegistry`, `ToolRegistry` with PR1 fake tools registered; `StartAgentRunHandler` uses registry; unknown unregistered tool path tested.
- **PR12** — `RuntimePolicyEvaluator` + `RuntimePolicyOptions` (`Agentor:RuntimePolicy`); allow / deny list / risk-based `RequiresReview`; `AllowAllPolicyEvaluator` removed; tests for allow, deny, requires-review, unknown tool.
- Verification: `scripts/capture-verification.ps1` run; evidence under `artifacts/verification/` read (including `eval-fixtures.txt`); `test-results.agentor.json` updated for `pr09-pr12-batch`.
- **Phase 17 PR81–PR85** — Enterprise policy model: `PolicyBundle` domain aggregate (versioned, immutable after publication, duplicate rule IDs rejected); `PolicyRule` entity with full kind/scope/effect taxonomy and factory helpers; `PolicyProfile` + `ActivePolicyProfile` domain types; `IPolicyBundleRepository` + `IPolicyProfileRepository` application abstractions; `PolicyBundleRulesAdapter` (bundle → `PolicyProfileRules`); `InMemoryPolicyBundleRepository` + `InMemoryPolicyProfileRepository`; `RuntimePolicyEvaluator` bundle-aware 2-constructor pattern; `PolicyProfileRules.RequiresReviewToolKeys`; `PolicyBundleDtos` + 4 API endpoints (`GET/POST /policy-bundles`, `GET /policy-bundles/{id}`, `POST /policy-profiles/{id}/activate`); audit export `policyIdentity` section; 38 new tests (25 domain + 13 evaluation); 3 deterministic fixture JSONs; `docs/developer/policy-bundles.md`. Grand total: **298 tests passing**.

## In progress

- None.

## Next

- Phase 18+ per roadmap when explicitly scheduled.

## Notes

Agentor service boundaries remain:

```text
Agentor executes.
Athanor canonizes.
Conexus routes models.
MCP connects tools later.
External frameworks are adapters, not core.
```

PR07 follow-ups (not blocking read APIs): prefer append/update persistence over delete/reinsert for audit semantics; add real PostgreSQL integration tests later (optional/skippable).
