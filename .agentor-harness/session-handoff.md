# Session handoff — Phase 26 PR117 (policy scope enforcement)

## Completed

- **Domain**: **`PolicyRuleScope.KnowledgeScope`**; **`PolicyRule`** optional scope identifier fields with validation; **`AgentRun.ToPolicyScope()`**.
- **Adapter**: **`PolicyBundleRulesAdapter.ToProfileRules(PolicyBundle, AgentRunScope)`** (+ overload without scope → empty run scope); MCP/external deny + model budget merges respect scope filtering and specificity/stricter thresholds.
- **Evaluation**: **`PolicyEvaluationRequest`** adds **`AgentRunScope? Scope`**; **`RuntimePolicyEvaluator`** passes scope into adapter when resolving active bundle.
- **Call sites**: **`SequentialAgentPlanExecutor`**, **`GovernedSingleToolRunDriver`**, **`ApplyHumanReviewDecisionHandler`** pass **`run.ToPolicyScope()`**.
- **API/contracts**: **`CreatePolicyRuleDto`** / **`PolicyRuleDto`** scope id fields; **`PolicyBundleEndpoints`** + **`DtoMappings`**.
- **Audit**: **`GetRunAuditExportQueryHandler`** root **`effectivePolicyScope`**; test **`HandleAsync_IncludesEffectivePolicyScopeObject`**.
- **Tests**: **`PolicyScopeEvaluationTests`** (tenant/workspace/project/global isolation + adapter filter); **`PolicyBundleTests`** validation cases.
- **Docs**: **`docs/REPO_TRUTH.md`**, **`docs/developer/policy-bundles.md`**, **`docs/RELEASE/v1.0-RC-DEFERRED-ITEMS.md`** (no active `passes:false` rows).
- **Harness**: **`SCOPE-001`** → **`passes: true`**; **PR117-001..003** acceptance rows; phase **26** / **PR117**.

## Verification

- `dotnet restore Agentor.sln` succeeded
- `dotnet build Agentor.sln --no-restore` succeeded
- `dotnet test Agentor.sln --no-build` succeeded (**428 passed, 0 failed**)
- `powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/verify-harness.ps1 -ExpectedPhase 26 -ExpectedHarnessPass PR117` succeeded
- `powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/verify-repo-clean.ps1` succeeded

## What is next

- **Phase 27** — EF persistence correctness / append-aware save / concurrency (per `docs/planning/pr76-125/Phase 23 - 31.md`), or next scheduled phase.

## What was explicitly not started

- **Phase 27+** was **not** started.

## Remaining risks / deferred

- **EF aggregate save** remains delete/reinsert-style in places (**REPO_TRUTH** persistence bullet).
- Historical harness notes under **`feature-list.json`** may still mention older “SCOPE-001 deferred” wording in archived phase bullets; current **`SCOPE-001`** acceptance row is **closed**.
