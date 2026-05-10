# Repository truth — current behavior

This document states **what the code and HTTP surface actually do today**, so operators and integrators do not mistake roadmap or internal components for default production behavior.

## Public agent runs

- **`POST /api/v1/agent-runs`** flows through **`IAgentRunOrchestrator`** (`AgentRunOrchestrator`). Clients select execution with **`planId`**, **`recipeId`**, **`toolKey`**, **`skillKey`**, and/or **`mode`** (see `StartAgentRunRequestDto`). **`RunExecutionMode.LegacyFakeTool`** (or **`Agentor:PublicRuns:TreatMissingExecutionSelectorAsLegacyFakeTool=true`**, the default) keeps the historical PR1 fake-tool path without specifying selectors.
- **Plan / recipe / skill** starts materialize a fresh `AgentPlan` and execute via **`SequentialAgentPlanExecutor`**. **Single-tool** paths (including Conexus, MCP, external-agent tool keys) use the shared governed single-tool driver (policy + pipeline) without embedding tool logic in `StartAgentRunHandler`.

## Real execution kernel

- **`SequentialAgentPlanExecutor`** is the coordinator for **plan-mode** public starts (and skill-wrapped ephemeral recipes). **Single-tool** public starts still use the same policy and **`IToolExecutionPipeline`** stack as plans, but do not route through the plan executor’s step loop.

## Policy scopes

- Policy rules carry **tenant / workspace / project / knowledge-scope** model fields, but **`PolicyBundleRulesAdapter` applies bundle rules globally** (no scope-identity filtering). Tracked as **SCOPE-001** in deferred items and harness acceptance.

## Persistence

- **PostgreSQL / EF** persistence exists, but aggregate persistence for runs still uses a **delete-then-reinsert style save** for the aggregate in places — not yet append-only / optimistic-concurrency hardened for audit-grade immutability expectations.

## Authentication (Jwt mode)

- **Jwt** auth mode consumes an **already-authenticated** `ClaimsPrincipal` (header or upstream gateway). The API does **not** register full **Bearer token validation middleware** by default in that configuration; treat Jwt mode as **explicit external auth** unless you add validation yourself.

## Where to read more

- `docs/planning/pr111-pr120.md` — consolidation plan (Phases 23–31).
- `docs/security/auth-boundary.md` — permission and auth-mode details.
- `docs/RELEASE/v1.0-RC-DEFERRED-ITEMS.md` — deferred gaps including SCOPE-001.
