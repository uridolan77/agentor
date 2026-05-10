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
- **Phase 17 PR85.5** — Policy deferred-item reconciliation: PR52-004 closed (PolicyBundle fully implemented by Phase 17); SCOPE-001 documented (PolicyRuleScope stored but not enforced — Tenant/Workspace/Project enforcement deferred to v1.1); scope limitation comment added to `PolicyBundleRulesAdapter.cs` and "Known limitations" section to `docs/developer/policy-bundles.md`; `feature-list.json` harnessPass=PR85.5, PR52-004 passes=true, SCOPE-001 passes=false. verify-harness + verify-repo-clean both passed.
- **Phase 18 PR86–PR90** — Multi-step human review resume semantics: `PlanResumeCursor` + `PendingPlanStep` + `ReviewResumeState` domain types; `AgentRun.RecordPlanResumeCursor/ClearResumeCursor`; `SequentialAgentPlanExecutor` records cursor on mid-plan `RequiresReview`; `ApplyHumanReviewDecisionHandler` resumes remaining steps (full failure-policy + RequiresReview chaining); 6 API integration tests; 2 eval fixtures (schema 5); PR53-005 closed with named evidence. Grand total: **331 tests passing**.
- **Phase 17 PR81–PR85** — Enterprise policy model: `PolicyBundle` domain aggregate (versioned, immutable after publication, duplicate rule IDs rejected); `PolicyRule` entity with full kind/scope/effect taxonomy and factory helpers; `PolicyProfile` + `ActivePolicyProfile` domain types; `IPolicyBundleRepository` + `IPolicyProfileRepository` application abstractions; `PolicyBundleRulesAdapter` (bundle → `PolicyProfileRules`); `InMemoryPolicyBundleRepository` + `InMemoryPolicyProfileRepository`; `RuntimePolicyEvaluator` bundle-aware 2-constructor pattern; `PolicyProfileRules.RequiresReviewToolKeys`; `PolicyBundleDtos` + 4 API endpoints (`GET/POST /policy-bundles`, `GET /policy-bundles/{id}`, `POST /policy-profiles/{id}/activate`); audit export `policyIdentity` section; 38 new tests (25 domain + 13 evaluation); 3 deterministic fixture JSONs; `docs/developer/policy-bundles.md`. Grand total: **298 tests passing**.

## In progress

- None.

## Next

- Phase 19 or next explicitly scheduled phase.

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
