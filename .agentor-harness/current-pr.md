# Current PR — harness marker

Completed: Phase 24 **PR112–PR115** — Public run **orchestration kernel**: `RunOrchestrationRequest` + `RunExecutionMode`; `LegacyFakeRunExecutor` + `GovernedSingleToolRunDriver`; **`IAgentRunOrchestrator` / `AgentRunOrchestrator`** (legacy fake, single-tool including Conexus/MCP/external keys, plan from store, recipe instantiate, skill ephemeral wrap); **`StartAgentRunHandler`** thin over `StartAgentRunRouting` + **`AgentorPublicRunOptions`**; **`StartAgentRunRequestDto`** extended (`mode`, `planId`, `recipeId`, `toolKey`, `skillKey`, `input`); **`RunOrchestrationValidationException`** → HTTP 400 via middleware; DI registers **`IAgentPlanExecutor`** / `SequentialAgentPlanExecutor`; **`AgentRunOrchestrationApiTests`**; **`docs/REPO_TRUTH.md`** + **README** updated; **`appsettings.json`** `Agentor:PublicRuns`. Verification: restore/build/test and harness scripts with **ExpectedPhase 24 / PR115**.

Next: Phase 25 (queue hosted-service scoped lifetimes) or next explicitly scheduled phase.

Do not start the next phase during closeout.
