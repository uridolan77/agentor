# Multi-Step Human Review Resume Semantics

**Phase 18 — PR86–PR90**

## Purpose

Close the governance/execution gap introduced by single-tool review: when policy flags a tool call mid-plan as `RequiresReview`, the existing executor suspends the run at exactly that tool call. After human approval, execution must resume from the suspended step and continue through all remaining plan steps in order — not just complete the one approved tool call.

---

## Concepts

### ReviewCheckpoint

A `ReviewCheckpoint` is the state captured when plan execution suspends pending human review. It records:

- Which plan step was blocked (the step whose tool policy returned `RequiresReview`)
- Which plan steps had already completed (with their outputs, for context rebuild)
- Which plan steps remain to execute (in order, after the blocked step)
- The timestamp of suspension

A checkpoint is attached to the run via `AgentRun.ResumeCursor` and serialized as a `PlanResumeCursor` domain record.

### ResumeCursor

The `PlanResumeCursor` is the durable pointer that identifies exactly where execution left off. It enables the approval handler to:

1. Identify the plan and the step that required review.
2. Rebuild execution context from completed-step history.
3. Continue executing remaining plan steps after approval.

A cursor exists only while the run is in `RequiresReview`. It is cleared immediately when multi-step resume begins. If a subsequent step also requires review, a new cursor is recorded for that step.

### PendingPlanStep

A `PendingPlanStep` is a lightweight snapshot of an `AgentPlanStep` that was not yet reached when execution suspended. It stores:

- Plan step ID, source step ID, order index
- Tool key and step kind (Tool or Skill)
- Failure handling policy
- Static input binding parameters (resolved at cursor creation time)
- Output binding (for context capture)

Static input parameters are sufficient because `PlanInputBuilder` uses only declared parameters plus session memory — there are no cross-step output references in input building.

### ReviewedToolContinuation

When approval arrives, the first action is executing the approved (previously-blocked) tool call. The output of this tool becomes part of the rebuilt `PlanExecutionContext` and is available to subsequent steps via session memory.

The continuation then iterates `PlanResumeCursor.RemainingSteps` in order, executing each with full policy evaluation and tool pipeline (same as the original executor path).

---

## Invariants

### Approval never overrides Deny

During resume of remaining plan steps, each tool call is re-evaluated by the policy engine. If policy returns `Deny` for a resumed step, that step fails and the run fails (or continues, based on the step's `OnFailure` policy). **An approval decision for the originally-blocked step grants no forward-looking license to subsequent tool calls.**

### Approval does not canonize knowledge

Human approval is a governance signal that execution may proceed. Tool outputs remain non-canon candidate material. No approval operation touches Athanor or any external knowledge-state service. This invariant is unchanged from the single-step review path (see `HumanReviewDecisionRecorded` trace event).

### Deny cannot be resumed

A run in `Failed` state cannot be resumed. Only runs in `RequiresReview` accept governance decisions. If a reviewer rejects a run that has a cursor, the run fails immediately and the cursor is irrelevant. The `AgentRun.ApplyHumanReviewDecision` state machine enforces this.

### Cursor cannot be created for completed or failed plans

`AgentRun.RecordPlanResumeCursor` validates that the run is in `RequiresReview` state before accepting a cursor. Calling it in any other state throws `InvalidOperationException`.

---

## FailFast / ContinueOnFailure / SkipRemaining Interactions During Resume

The `OnFailure` policy on each `PendingPlanStep` governs what happens if that step fails during resumed execution:

| `OnFailure`              | Step fails during resume                          |
|--------------------------|---------------------------------------------------|
| `FailFast` (default)     | Run fails immediately. No further steps execute.  |
| `ContinueOnFailure`      | Step is recorded as failed; next step executes.   |
| `SkipRemaining`          | Remaining steps (after the failed one) are skipped; run completes. |
| `EscalateToReview`       | Run re-enters `RequiresReview` with the failed/denied resumed tool left as a pending reviewable tool call; a new cursor is recorded for any remaining later steps. |
| `MarkForCompensation`    | Step is recorded as failed; next step executes.   |
| `RetryViaToolPipelineOnly` | Retry is handled inside the tool execution pipeline before resume sees the result. |

---

## Multi-Step RequiresReview Chaining

If a resumed step itself triggers `RequiresReview`, execution suspends again. A new `PlanResumeCursor` is recorded for the newly-blocked step (with the steps remaining after it). The run returns to `RequiresReview`. The next approval resumes from the new cursor. This chain allows multiple sequential review gates in one plan.

When a resumed step reaches `EscalateToReview` because policy denies it or the tool execution fails, the runtime uses the same reviewable-tool contract as the main review path:

- The resumed step is left in `RequiresReview`.
- The tool call is left in `ToolCallStatus.RequiresReview` rather than a terminal denied/failed state.
- `AgentRun.ApplyHumanReviewDecision()` can therefore act on the state safely using the normal approve/reject workflow.

Approval still does not override policy denial. If the reviewer approves a resumed tool that policy continues to deny, the follow-up execution fails normally. For tool execution failure, approval retries the pending tool through the normal tool pipeline before any later steps resume.

---

## Phase 18 Limitations

- **EF Core persistence**: The `PlanResumeCursor` is an in-memory domain property. EF Core persistence of the cursor is deferred to v1.1. Tests use the in-memory repository where the cursor is naturally preserved within a single process.
- **Skill step resume**: When a `PendingPlanStep.Kind == Skill` is encountered during resumed execution, the runtime logs a `PlanExecutionFailed` trace event and applies the step's `OnFailure` policy. Full skill step resume is deferred to v1.1.
- **Guard re-evaluation**: Step guards are not re-evaluated during resumed execution in Phase 18. Guards are pre-execution checks intended for fresh plan starts. Resuming after human approval implies governance has already been applied to the suspended step.
- **Input binding resolution**: Input is rebuilt from static binding parameters plus the run's current session memory. Cross-step output references in input bindings are not supported in Phase 18 (they are not used by the current `PlanInputBuilder` either).
- **Non-resume escalation parity**: `EscalateToReview` semantics outside the resumed-step path still share older executor logic and should be kept aligned in a future no-behavior-change cleanup pass.

---

## Trace Events

| Event | When |
|-------|------|
| `PlanResumeCursorRecorded` | After plan step enters `RequiresReview` and remaining steps exist |
| `MultiStepPlanResumed` | At start of resumed remaining-step execution |
| `PlanResumeCursorCleared` | Immediately before resumed execution begins |
| `PlanExecutionStepCompleted` | Each resumed step that succeeds |
| `PlanExecutionFailed` | Skill step encountered during resume, or tool denied/failed with FailFast |
| `PlanExecutionCompleted` | All remaining steps complete without terminal failure |

---

## Evaluation Evidence

PR90 adds two deterministic fixtures:

- `review-gated-multistep-plan.json` — proves a 3-step plan suspends at step 2 (RequiresReview) and resumes to complete step 3 after approval.
- `review-resume-audit-export.json` — audit export showing `HumanReviewDecisionRecorded` followed by `MultiStepPlanResumed` and `PlanExecutionCompleted` trace events.

These fixtures close `PR53-005` in `feature-list.json`.
