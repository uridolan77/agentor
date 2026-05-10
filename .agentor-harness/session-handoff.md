# Session handoff

## Completed (PR75.8)

- `tests/Agentor.Api.Tests/AthanorRunningRunApiTests.cs`: POST evidence-provenance **204** and POST candidates **202** with run in **Running** state.
- `tests/Agentor.Api.Tests/Support/TestAgentRunRepository.cs` + `AthanorRunningRunApiFixture.cs`: replace `IAgentRunRepository` and seed `AgentRun.Start(...)` (POST `/api/v1/agent-runs` completes immediately so it cannot leave a running run).
- `.agentor-harness/feature-list.json`: **PR23-API-003** / **PR24-API-003** `passes: true`; **harnessPass PR75.8**; PR75.8 acceptance rows.
- `docs/RELEASE/v1.0-RC-DEFERRED-ITEMS.md`: only **PR52-004** and **PR53-005** remain documented as false.

## Not started

- **Phase 16+** product roadmap (unless explicitly scheduled).
- **v1.1** full PolicyBundle / enterprise policy engine (**PR52-004** still false).
- **v1.1** multi-step review resume semantics (**PR53-005** still false).

## Remaining risks / false acceptance

- See `feature-list.json` for **PR52-004** and **PR53-005** (`passes: false`); both deferred by design, not bugs introduced in PR75.8.