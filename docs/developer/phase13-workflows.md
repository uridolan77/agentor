# Developer workflows for Phase 13 surfaces

This note ties **code locations** to **operator workflows** without duplicating OpenAPI (generate or inspect routes from `Program.cs` and `Phase13ProductEndpoints.cs`).

## 1. Declare artifacts (management API)

1. `POST /api/v1/recipes` with a valid recipe body (tool or coordination steps as supported by contracts).
2. `POST /api/v1/plans` with `CreatePlanFromRecipeRequestDto` referencing the stored recipe id.
3. Optionally `POST /api/v1/skills` and `POST /api/v1/policy-profiles` for catalog and policy declaration data.

None of these endpoints execute a plan; they only validate and persist management records (in-memory stores in default PR1-style hosting).

## 2. Run the default deterministic agent

`POST /api/v1/agent-runs` with `StartAgentRunRequestDto` runs the built-in PR1-style path (policy-governed fake tool). Use standard read APIs under `/api/v1/agent-runs/...` for trace, steps, manifest, audit export.

## 3. Inspect a run via product aliases

After a run id exists:

- `GET /api/v1/runs/{id}/timeline`
- `GET /api/v1/runs/{id}/coordination-view` (enriched when plan id appears in trace and a matching plan exists in the plan store)
- `GET /api/v1/runs/{id}/audit-packet` (compare SHA-256 with `/agent-runs/{id}/audit-export`)

## 4. Model and Athanor-shaped calls (fake adapters)

With integrations in **Fake** mode (default in sample `appsettings.json`):

- Model completion tool path is documented under Conexus boundary docs; requests still go through tool registry and policy.
- Athanor evidence and candidate routes remain **non-canon**: they attach provenance or staging artifacts to the run trace as designed in earlier phases.

## 5. Reviews

- Inbox: `GET /api/v1/reviews/pending`
- Decision: `POST /api/v1/reviews/{runId}/decisions` (same semantics as `/agent-runs/.../human-review`)

For automated tests of the resume and deny-after-approve behavior, see `tests/Agentor.Domain.Tests/AgentRunTests.cs` and `tests/Agentor.Application.Tests/ApplyHumanReviewDecisionHandlerTests.cs`.

## Boundary reminder

If documentation or examples ever imply that Agentor **stores canonical facts** or **replaces** Athanor, that documentation is wrong. Agentor executes and observes; Athanor owns canonical knowledge-state.
