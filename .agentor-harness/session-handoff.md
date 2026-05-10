# Session handoff — Phase 26 PR117 + PR117.5

## Completed

- **PR117 (scoped policy)** — unchanged baseline: **`PolicyRule`** scope identifiers + **`KnowledgeScope`**, **`PolicyBundleRulesAdapter.ToProfileRules(bundle, AgentRunScope)`**, **`PolicyEvaluationRequest.Scope`**, **`RuntimePolicyEvaluator`**, **`AgentRun.ToPolicyScope()`** call sites, audit **`effectivePolicyScope`**, **`SCOPE-001`** closed in deferred-items harness.
- **PR117.5 (orchestration + queue payload hardening)**:
  - **EF queue**: **`RunQueueItemRecord`** + **`EfRunQueueStore`** persist **`ExecutionMode`**, **`RecipeId`**, **`PlanId`**, **`ToolKey`**, **`SkillKey`**, **`ToolInputJson`**; migration **`20260511183000_RunQueueOrchestrationPayload`** + **`AgentorDbContextModelSnapshot`** **`run_queue_items`** entity.
  - **Fingerprint**: **`StartAgentRunFingerprint`** includes **`TenantId` / `WorkspaceId` / `ProjectId` / `KnowledgeScopeId`** (deterministic **`Guid`** **`D`** tokens).
  - **HTTP errors**: **`RunOrchestrationNotFoundException`** → **404** with stable **`ReasonCode`**; **`RunOrchestrationValidationException`** remains **400**; generic **`Exception`** → **500** with fixed **`AgentorUnhandledError`** message (no raw exception text).
  - **Orchestrator**: replaces former **`InvalidOperationException`** throws on expected validation/not-found paths with the typed exceptions above.
  - **Tests**: **`EfRunQueueStoreTests.EnqueueAsync_RoundTripsOrchestrationSelectorsAndToolInput`**; **`RunQueueHostedServiceEfSqliteScopeTests`** (model, MCP echo, legacy explicit, recipe); **`ApiContractTests`** idempotency scope conflicts; **`AgentRunOrchestrationApiTests`** unknown plan/recipe/skill.
  - **Docs**: **`docs/REPO_TRUTH.md`** durable-queue payload bullet; **`docs/developer/policy-bundles.md`** scoped-merge security note (specific Allow vs Global Deny).

## Verification

- `dotnet restore Agentor.sln` succeeded
- `dotnet build Agentor.sln --no-restore` succeeded
- `dotnet test Agentor.sln --no-build` succeeded (**438 passed, 0 failed**)
- `powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/verify-harness.ps1 -ExpectedPhase 26 -ExpectedHarnessPass PR117.5` succeeded
- `powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/verify-repo-clean.ps1` succeeded

## What is next

- **Phase 27** — EF persistence correctness / append-aware aggregate save / concurrency (per planning doc), or next scheduled phase.

## What was explicitly not started

- **Phase 27+** broader persistence refactor was **not** started (PR117.5 only).

## Remaining risks / deferred

- **EF aggregate save** remains delete/reinsert-style in places (**REPO_TRUTH** persistence bullet).
- **PR117.5** does not add an automated test that asserts the **500** response body text (covered by code review + harness acceptance **`PR117.5-005`** citing **`ExceptionHandlingMiddleware`**).
