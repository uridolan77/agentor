# Current PR — harness marker

Completed: Phase 26 **PR117** — **Policy scope enforcement (SCOPE-001 closed)**: **`PolicyRule`** gains validated **`ScopeTenantId` / `ScopeWorkspaceId` / `ScopeProjectId` / `ScopeKnowledgeScopeId`** and **`PolicyRuleScope.KnowledgeScope`**; **`PolicyBundleRulesAdapter.ToProfileRules(bundle, AgentRunScope)`** filters + merges by specificity (**KnowledgeScope > Project > Workspace > Tenant > Global**) with tie-break **Deny > RequiresReview > Allow** for tool access; **`PolicyEvaluationRequest.Scope`** + **`AgentRun.ToPolicyScope()`** through plan executor, single-tool driver, and review resume; **`RuntimePolicyEvaluator`** resolves active bundle with run scope; audit export **`effectivePolicyScope`**; **`CreatePolicyRuleDto` / `PolicyRuleDto`** extended; **`PolicyScopeEvaluationTests`** + domain validation tests; **`docs/REPO_TRUTH.md`**, **`docs/developer/policy-bundles.md`**, **`docs/RELEASE/v1.0-RC-DEFERRED-ITEMS.md`**. Verification: restore/build/test — **428 passed**; harness scripts **ExpectedPhase 26 / PR117**.

Next: Phase 27 (persistence / append-aware EF save) or next explicitly scheduled phase.

Do not start the next phase during closeout.
