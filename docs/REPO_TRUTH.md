# Repository truth — current behavior

This document states **what the code and HTTP surface actually do today**, so operators and integrators do not mistake roadmap or internal components for default production behavior.

## Public agent runs

- **`POST /api/v1/agent-runs`** flows through **`IAgentRunOrchestrator`** (`AgentRunOrchestrator`). Clients select execution with **`planId`**, **`recipeId`**, **`toolKey`**, **`skillKey`**, and/or **`mode`** (see `StartAgentRunRequestDto`). **`RunExecutionMode.LegacyFakeTool`** (or **`Agentor:PublicRuns:TreatMissingExecutionSelectorAsLegacyFakeTool=true`**, the default) keeps the historical PR1 fake-tool path without specifying selectors.
- **Plan / recipe / skill** starts materialize a fresh `AgentPlan` and execute via **`SequentialAgentPlanExecutor`**. **Single-tool** paths (including Conexus, MCP, external-agent tool keys) use the shared governed single-tool driver (policy + pipeline) without embedding tool logic in `StartAgentRunHandler`.

## Real execution kernel

- **`SequentialAgentPlanExecutor`** is the coordinator for **plan-mode** public starts (and skill-wrapped ephemeral recipes). **Single-tool** public starts still use the same policy and **`IToolExecutionPipeline`** stack as plans, but do not route through the plan executor’s step loop.
- **Structured tool I/O (Phase 30 / PR121)**: **`ToolPayload`** (**JSON `body`**, optional **`schemaId`/`contentType`**, flat **`summary`**) is the execution envelope for **`ToolExecutionRequest`**/**`ToolCall`** persistence; **`ToolPayload.FromLegacyDictionary`**/**`ToLegacySummary`**/**`ToPolicyEvaluationDictionary`** bridge PR1-style flat maps. Integration DTOs (**`ModelCallRequestDto`/`ModelCallResultDto`**, **`ExternalAgentInvocationRequestDto`**, MCP HTTP invoke) serialize **`ToolPayload`** as JSON; audit export emits **`input`/`output`** objects with **`body`** + **`summary`** (redaction applies to nested JSON).

## Policy scopes

- **`PolicyRule`** rules carry **`Scope`** (`Global`, `Tenant`, `Workspace`, `Project`, **`KnowledgeScope`**) plus optional **scope identifier** fields (`ScopeTenantId`, `ScopeWorkspaceId`, `ScopeProjectId`, `ScopeKnowledgeScopeId`) validated at construction.
- **`PolicyBundleRulesAdapter.ToProfileRules(bundle, AgentRunScope)`** includes only rules whose scope matches the run’s **`TenantId` / `WorkspaceId` / `ProjectId` / `KnowledgeScopeId`**. Conflicts merge by **specificity** (KnowledgeScope → Project → Workspace → Tenant → Global); at equal specificity, **Deny > RequiresReview > Allow** for tool-access rules.
- **`PolicyEvaluationRequest`** includes **`AgentRunScope`** so **`RuntimePolicyEvaluator`** builds the effective profile per tool call from the active bundle and the run’s identity.

## Persistence

- **`EfCoreAgentRunRepository.SaveAsync`** merges into the existing **`agent_runs`** row: upserts root scalars (including **`session_memory_json`**, **`human_review_decisions_json`**, and **`resume_cursor_json`** for **`PlanResumeCursor`**), upserts **`agent_steps`** and nested **`tool_calls`** / **`policy_decisions`** by id, and **appends** new **`trace_events`** by id. Existing trace rows are **immutable**; a save that would rewrite payload raises **`AgentRunTraceImmutabilityException`**.
- **`agent_runs.aggregate_version`** is an optimistic-concurrency token (incremented on each successful save). **`AgentRun.PersistenceConcurrencyVersion`** is populated on **`GetAsync`** and refreshed after **`SaveAsync`**; a stale version yields **`AgentRunPersistenceConcurrencyException`** (mapped to **409 Conflict** on the HTTP surface). **`POST /agent-runs`** and other handlers that only create runs do not require a prior load.
- **Human review workflow (Phase 28)**: **`completed_at`** stores **successful completion only**. Review suspension uses **`review_requested_at`** / **`paused_at`**; failures and human rejection terminal timestamps use **`terminal_at`**. **`review_workflow_status`** persists **`HumanReviewWorkflowStatus`** (Pending / ChangesRequested / Escalated / …). **`ApplyHumanReviewDecision`** APIs accept optional **`relatedPriorActorId`** for escalation chains.

## Authentication (Jwt / ASP.NET)

- **`UseAuthentication` / `UseAuthorization`** are enabled. **`/api/v1/*`** requires an authenticated principal (**`Agentor.Authenticated`** policy). **`GET /health`** stays anonymous.
- **Fake** mode registers **`Agentor.Fake`** authentication (stable dev principal). **Header** mode registers **`Agentor.Header`** (GUID in `X-Agentor-Actor-Id` by default). **Jwt** mode registers **`AddJwtBearer`** when **`Agentor:Auth:JwtAuthority`** is set; otherwise **`Agentor:Auth:JwtAcceptUnvalidatedBearerTokens=true`** enables **`Agentor.JwtUnvalidated`** (parses bearer JWTs **without** signature validation — trusted path only).
- **`ICurrentActorAccessor`** still maps **`HttpContext.User`** in Jwt mode (claims → `ActorRole`). See **`docs/security/auth-boundary.md`** and **`docs/security/AUTHORIZATION_MATRIX.md`**.

## Durable run queue worker

- **`RunQueueHostedService`** is registered as a **hosted singleton** but resolves **`IDurableRunQueue`**, **`IRunExecutionLeaseStore`**, and **`IAgentRunOrchestrator`** inside a **per-drain `IServiceScopeFactory` async scope**, so EF-backed scoped implementations share one **`DbContext`** for claim → execute → mark complete/failed. Drained items execute through **`StartAgentRunRouting`** + **`IAgentRunOrchestrator`** (not a separate fake-centered path). **`AddAgentorInfrastructure`** binds **`Agentor:PublicRuns`** so worker routing defaults match API configuration.
- **EF `run_queue_items`** rows persist **governance scope** (`tenant_id`, `workspace_id`, `project_id`, `knowledge_scope_id`) and **public orchestration selectors** (`execution_mode`, `recipe_id`, `plan_id`, `tool_key`, `skill_key`, plus **`tool_input_json`** for string-keyed tool inputs) so queued work replays the same routing as inline **`POST /api/v1/agent-runs`** starts after dequeue (PR117.5).

## Where to read more

- `docs/planning/pr111-pr120.md` — consolidation plan (Phases 23–31).
- `docs/security/auth-boundary.md` — permission and auth-mode details.
- `docs/RELEASE/v1.0-RC-DEFERRED-ITEMS.md` — deferred gaps including SCOPE-001.
