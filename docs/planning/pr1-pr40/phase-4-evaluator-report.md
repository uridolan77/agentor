# Phase 4 evaluator report (PR16 through PR20)

UTF-8. Written as part of **PR20.5** (cleanup); the test-gap section was updated in **PR20.6**. Not a product runtime document.

## Scope result

Phase 4 stayed within Agentor coordination: recipes, plan instantiation, sequential execution, deterministic guards, failure-handling metadata, compensation hook recording, and run/plan/step state machine hardening. No Athanor, Conexus, MCP, LLM calls, or third-party agent frameworks were added.

## Tests observed (this repo)

| Area | File | Role |
| --- | --- | --- |
| Domain recipe/plan | `tests/Agentor.Domain.Tests/AgentRecipePlanDomainTests.cs` | Validation, ordering, guard rejection on create |
| Domain state machine | `tests/Agentor.Domain.Tests/AgentStateMachineTests.cs` | Illegal transitions for skipped steps, denied/review tool calls, terminal run |
| Application executor | `tests/Agentor.Application.Tests/SequentialAgentPlanExecutorTests.cs` | Order, Deny vs RequiresReview, guards, skip-remaining, continue-on-failure path, compensation metadata |

## Test gaps

### Closed in PR20.6 (tests added)

- **Trace discipline** (`PR17-007`): `SequentialAgentPlanExecutorTests.TwoStepPlanSuccess_PlanTraceKindsAppearInStrictCoordinatorOrder` asserts strict coordinator order of trace kinds for a two-step success path.
- **Partial failure matrix** (`PR19-005`): `SequentialAgentPlanExecutorTests.FirstStepToolFailure_OnFailurePolicyMatrix` covers FailFast, ContinueOnFailure, and SkipRemaining for first-step tool failure (not an exhaustive policy-by-outcome grid).
- **Success semantics** (`PR20-006`): `SequentialAgentPlanExecutorTests.ContinueOnFailure_CompletedRun_PlanResultSuccess_WithFailedFirstPlanStep` asserts coordinator success with run `Completed` while the plan is `Failed` under tolerated step failure.

### Still open

- **API**: HTTP contract tests for plan/recipe execution paths (`Agentor.Api.Tests` gap; see harness `PR16-009`, `PR17-008`).
- **Persistence**: Repository round-trip for plans/runs/steps relevant to coordination (`PR16-010`).
- **Exhaustive failure matrix**: All `FailureHandlingPolicy` combinations with every tool and policy outcome (beyond the PR20.6 first-step matrix).
- **Cross-layer invariant**: All plan steps terminal before run completion, asserted outside executor unit tests alone (`PR20-005`).
- **Step-guard matrix**: Exhaustive combinations of `StepGuardKind` with policy outcomes (`PR18-004`).
- **Compensation hooks**: Execution semantics beyond trace metadata (`PR19-006`).

## Risks before PR21

1. **Plan health vs run completion**: A run can complete while tolerated plan steps failed. Consumers must use `PlanStatus`, `StepResults`, and `PlanFailureSummary`, not `RunStatus.Completed` alone.
2. **Executor size**: `SequentialAgentPlanExecutor` bundles policy, pipeline, guards, failure policy, tracing, and finalization. Splitting into internal types is deferred behind a source TODO until a low-risk refactor window.
3. **Harness hygiene**: UTF-16-without-BOM text breaks JSON tooling; PR20.5 normalizes harness files to UTF-8.

## Cleanup recommendations

- Keep `feature-list.json` item-level; never mark `passes: true` without a named test or evidence string.
- After PR20.5 merge, close remaining test gaps above before expanding coordination surface toward Athanor.

## PR21 confirmation

**PR21 Athanor integration was not started** for this change set. **PR20.6** added Application tests and harness doc updates only; no Athanor adapter or runtime integration work.
