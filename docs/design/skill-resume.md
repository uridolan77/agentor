# Skill procedure resume (Phase 34 / PR133)

This note describes how Agentor resumes execution when a **sequential plan** suspends for human review **inside a skill procedure** (an inner governed tool call), and how that differs from plain multi-step tool resume.

## Model (`Agentor.Domain.Governance`)

- **`SkillInnerToolCheckpoint`**: identifies the blocked inner tool (`ProcedureStepId`, `ToolKey`, `ProcedureOrderIndex`).
- **`SkillProcedureResumeState`**: non-canon scratch state (for example, the last inner tool’s flat output map) carried across the suspension boundary. Tool outputs remain **evidence**, not Athanor canon.
- **`SkillResumeCursor`**: bundles the **pending skill plan step** (`PendingPlanStep` with `InvokedSkillKey` / `InvokedSkillVersion`), the checkpoint, and the procedure state.
- **`PlanResumeCursor`**: adds optional **`SkillContinuation`**. **`HasContinuationWork`** is true when there are **remaining plan steps after the blocked step** **or** a **`SkillContinuation`** is present (so a skill can be the last plan step and still resume).

## Approval semantics (PR133 invariants)

- **Approval does not grant forward license**: post-approval policy re-evaluation uses **`ResumeAfterApprovedHumanReview`** on the **blocked inner tool only**. Subsequent inner tools in the procedure are evaluated normally (no blanket allow).
- **Non-canon**: skill procedure state and inner outputs are runtime coordination data; they are not written as Athanor knowledge state by this path.

## Runtime flow (Application)

1. **`SequentialAgentPlanExecutor`** records a **`PlanResumeCursor`** with **`SkillContinuation`** when review suspends mid-skill while optional tail plan steps remain (or skill-only continuation when the skill is the last plan step—see domain guard).
2. **`ReviewedToolContinuationService`** completes the approved inner **`ToolCall`**, then calls **`IAgentPlanExecutor.ContinueSkillProcedureAfterInnerToolApprovalAsync`**, which continues the skill procedure, completes the **plan-level skill step** once, and emits **`RecordStepCompletedAfterReview`** without double-completing the same **`AgentStep`**.
3. If the cursor had **tail plan steps**, a synthetic **`PlanResumeCursor`** with **`SkillContinuation: null`** is passed to **`PlanResumeOrchestrator.ResumeRemainingPlanStepsAsync`**, which applies the same failure policies as any other resumed plan segment (**FailFast**, **ContinueOnFailure**, **SkipRemaining**, **MarkForCompensation**, **EscalateToReview**).

## Persistence

**`resume_cursor_json`** round-trips **`SkillContinuation`** with the rest of **`PlanResumeCursor`** (see infrastructure tests).
