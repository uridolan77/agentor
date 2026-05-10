# Session handoff — Phase 24 PR115 (public run orchestration kernel)

## Completed

- **Orchestration model**: `RunExecutionMode` + `RunOrchestrationRequest`; `StartAgentRunRouting` + `AgentorPublicRunOptions` (`Agentor:PublicRuns:TreatMissingExecutionSelectorAsLegacyFakeTool`, default true for backward compatibility).
- **Executors**: `GovernedSingleToolRunDriver` (policy + pipeline single tool); `LegacyFakeRunExecutor` (PR1 fake path); **`AgentRunOrchestrator`** implements **`IAgentRunOrchestrator`** (legacy, single-tool / model / MCP / external, plan from store, recipe instantiate, skill wrap).
- **API**: `StartAgentRunRequestDto` extended; `StartAgentRunRequestMapping`; `StartAgentRunFingerprint` includes routing fields; **`RunOrchestrationValidationException`** → **400** in `ExceptionHandlingMiddleware`.
- **DI**: `IAgentPlanExecutor` → `SequentialAgentPlanExecutor`, `GovernedSingleToolRunDriver`, `LegacyFakeRunExecutor`, `IAgentRunOrchestrator`; `StartAgentRunHandler` now takes orchestrator + options monitor.
- **Tests**: `AgentRunOrchestrationApiTests` (Conexus, MCP echo, external invoke, deny, requires-review, explicit legacy); `AgentorTestComposition` + `StartAgentRunTestFactory` for handler construction; **419** tests passing solution-wide.
- **Docs**: `docs/REPO_TRUTH.md`, `README.md` (limitations + API examples).

## Verification

- `dotnet restore Agentor.sln` succeeded
- `dotnet build Agentor.sln --no-restore` succeeded
- `dotnet test Agentor.sln --no-build` succeeded (**419 passed, 0 failed**)
- `powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/verify-harness.ps1 -ExpectedPhase 24 -ExpectedHarnessPass PR115` succeeded
- `powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/verify-repo-clean.ps1` succeeded

## What is next

- **Phase 25** — `RunQueueHostedService` scoped queue/lease resolution; queue invokes orchestrator (per `docs/planning/pr111-pr120.md`), or next scheduled phase.

## What was explicitly not started

- **Phase 25+** (queue lifetime refactor, EF queue payload for full orchestration selectors, etc.) was **not** started.

## Remaining risks / deferred

- **SCOPE-001** remains **`passes: false`** (policy scope modeled, not enforced on evaluation).
- **Durable queue** persistence still stores the **narrow** `StartAgentRunCommand` columns only — advanced selectors on queued runs are not carried through EF queue rows until a later migration/phase.
