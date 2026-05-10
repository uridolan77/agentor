# Current PR — harness marker

Completed: Phase 28 **PR119** — **Review workflow semantics**: **`AgentRun`** separates **`ReviewRequestedAt`** / **`PausedAt`** / **`TerminalAt`** vs **`CompletedAt`** (success-only); **`HumanReviewWorkflowStatus`** on aggregate + API/audit; **`EnterRequiresReview`** no longer sets **`CompletedAt`**; **`Fail`** / human **`Reject`** use **`TerminalAt`**; **`ApplyHumanReviewDecisionHandler`** validates **`RequestChanges`**/**`Escalate`** notes; **`Escalated`** **`Approve`** requires **`ActorRole.HumanGovernanceApprover`** or **`System`**; **`HumanReviewDecision.RelatedPriorActorId`** + **`ApplyHumanReviewRequestDto`**; **`PendingHumanReviewItemDto`** exposes **`pausedAt`**/**`reviewRequestedAt`**/**`reviewWorkflowStatus`**; migration **`20260511203000_Phase28ReviewWorkflowSemantics`** + snapshot + legacy **`completed_at`** repair SQL; **`RoleBasedAuthorizationDecisionService`** allows governance approver; **`CoordinationEvaluationMetrics`** latency uses **`CompletedAt ?? TerminalAt`**. Docs **`docs/REPO_TRUTH.md`**. Verification: restore/build/test — **449 passed**; harness scripts **ExpectedPhase 28 / PR119**.

Next: Phase 29 (production auth boundary) or next explicitly scheduled phase.

Do not start the next phase during closeout.
