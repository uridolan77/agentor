
---

# Phase 18 — Multi-step human review resume semantics

**PR86–PR90**

Purpose: close the remaining governance/execution gap: review approval should resume multi-step plans correctly, not only a single pending tool.

## PR86 — Resume model design and invariants

Add design doc first:

```text
docs/design/multi-step-review-resume.md
```

Define:

```text
ReviewCheckpoint
ResumeCursor
PendingPlanStep
ReviewedToolContinuation
```

Acceptance:

```text
- Document defines FailFast, ContinueOnFailure, SkipRemaining interactions.
- Document says approval never overrides Deny.
- Document says approval does not canonize knowledge.
```

## PR87 — Domain resume cursor

Add domain structures:

```text
PlanResumeCursor
ReviewResumeState
```

Acceptance:

```text
- Cursor identifies plan step and pending tool.
- Cursor cannot resume completed/failed terminal plans.
- Tests cover invalid transitions.
```

## PR88 — Executor resume path

Add resume support to sequential plan execution.

Acceptance:

```text
- Approved review resumes from the pending step.
- Remaining steps execute in order.
- Tolerated failures preserve existing semantics.
- Deny cannot be resumed.
```

## PR89 — API review resume integration

Extend review decision endpoint to trigger multi-step resume when applicable.

Acceptance:

```text
- Approve resumes multi-step plan.
- Reject fails reviewed run.
- RequestChanges and Escalate record decision without executing tools.
- API tests cover all paths.
```

## PR90 — Evaluation and audit fixtures for resume

Add fixtures:

```text
review-gated-multistep-plan.json
review-resume-audit-export.json
```

Acceptance:

```text
- Evaluation snapshot proves resumed plan completion.
- Audit export shows review decision and resumed continuation.
- PR53-005 becomes true only with named evidence.
```
