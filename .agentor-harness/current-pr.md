# Current PR — harness marker

Completed: Phase 26 **PR117** — **Policy scope enforcement (SCOPE-001 closed)** plus **PR117.5** — **orchestration + queue payload hardening**: EF **`run_queue_items`** persists **`execution_mode`**, **`recipe_id`**, **`plan_id`**, **`tool_key`**, **`skill_key`**, **`tool_input_json`** (`RunQueueItemRecord`, **`EfRunQueueStore`**, migration **`20260511183000_RunQueueOrchestrationPayload`**, snapshot); **`StartAgentRunFingerprint`** includes governance scope GUIDs; **`RunOrchestrationNotFoundException`** + **`ExceptionHandlingMiddleware`** (**404** for missing plan/recipe/skill registrations; **500** returns generic message); **`AgentRunOrchestrator`** drops **`InvalidOperationException`** for expected selector/runtime validation paths; tests (**`EfRunQueueStoreTests`**, **`RunQueueHostedServiceEfSqliteScopeTests`**, **`ApiContractTests`** idempotency scope conflicts, **`AgentRunOrchestrationApiTests`** not-found cases); docs **`docs/REPO_TRUTH.md`**, **`docs/developer/policy-bundles.md`** (scoped merge security note). Verification: restore/build/test — **438 passed**; harness scripts **ExpectedPhase 26 / PR117.5**.

Next: Phase 27 (persistence / append-aware EF save) or next explicitly scheduled phase.

Do not start the next phase during closeout.
