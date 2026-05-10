# Agentor harness progress

## Phase 15 + PR75.8 (2026-05-10)

Completed **PR75.8** after **PR75.7**: closes Athanor API acceptance gaps **PR23-API-003** and **PR24-API-003** using `WebApplicationFactory` + `TestAgentRunRepository` to seed an `AgentRun` in **Running** (default POST `/api/v1/agent-runs` completes synchronously, so it cannot leave a running run).

- New tests: `tests/Agentor.Api.Tests/AthanorRunningRunApiTests.cs` (204 No Content on evidence-provenance, 202 Accepted on candidates).
- Support types: `tests/Agentor.Api.Tests/Support/TestAgentRunRepository.cs`, `AthanorRunningRunApiFixture.cs`.

**Not started:** Phase 16+ roadmap / v1.1 PolicyBundle / multi-step review resume (still tracked as false in harness where applicable).

Next harness marker: post–Phase 15 work when scheduled; do not start the next phase during closeout.
