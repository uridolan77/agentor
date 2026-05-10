# Session handoff — Phase 28 PR119

## Completed

- **Timeline semantics**: **`CompletedAt`** success-only; **`TerminalAt`** for failures / human rejection; **`ReviewRequestedAt`** / **`PausedAt`** for review suspension; **`EnterRequiresReview`** stops setting **`CompletedAt`**.
- **Workflow state**: **`HumanReviewWorkflowStatus`** (`AgentRun.ReviewWorkflowStatus`) distinct from **`AgentRunStatus`**; persisted as **`review_workflow_status`**.
- **Decisions**: **`RequestChanges`** / **`Escalate`** require non-empty notes; **`Escalated`** + **`Approve`** requires **`HumanGovernanceApprover`** or **`System`**; optional **`RelatedPriorActorId`** on **`HumanReviewDecision`** / **`ApplyHumanReviewRequestDto`**.
- **Surfaces**: **`AgentRunDto`** / **`AgentRunSummaryDto`** / **`PendingHumanReviewItemDto`** extended; **`ListPendingHumanReviewsQueryHandler`** maps workflow fields; audit export root includes **`terminalAt`**, **`reviewWorkflowStatus`**, decision **`relatedPriorActorId`**.
- **Authorization**: **`ActorRole.HumanGovernanceApprover`**; **`RoleBasedAuthorizationDecisionService`** treats like operator for permissions.
- **Persistence**: EF **`AgentRunRecord`** columns; **`RecordMapper`** round-trip; migration **`20260511203000_Phase28ReviewWorkflowSemantics`** + data repair for legacy **`completed_at`** misuse on **`RequiresReview`**/**`Failed`**/**`Completed`** rows.
- **Tests**: domain **`AgentRunTests`**; **`ApplyHumanReviewDecisionHandlerTests`** (workflow + escalation gate); **`MultiStepReviewResumeTests`** workflow asserts; **`GetRunAuditExportQueryHandlerTests`** audit chain; contracts round-trip; **`EfCoreAgentRunRepositoryTests`** HR reconstitute paths.

## Verification

- `dotnet restore Agentor.sln` succeeded
- `dotnet build Agentor.sln --no-restore` succeeded
- `dotnet test Agentor.sln --no-build` succeeded (**449 passed, 0 failed**)
- `powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/verify-harness.ps1 -ExpectedPhase 28 -ExpectedHarnessPass PR119` succeeded
- `powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/verify-repo-clean.ps1` succeeded

## What is next

- **Phase 29** — production auth boundary (per planning doc), or next explicitly scheduled phase.

## What was explicitly not started

- **Phase 29+** product work was **not** started.

## Remaining risks / deferred

- **Dashboard UX**: inbox API now exposes **`reviewWorkflowStatus`** for filtering “escalated only”; no dedicated HTTP filter parameter was added (clients filter client-side).
- **JWT deployments** must issue **`HumanGovernanceApprover`** in the configured role claim when principals should approve escalated reviews.
