# The real blockers now

## 1. Public execution is still fake-centered

This is the highest priority. Agentor already has a real sequential plan executor, with policy evaluation, skill invocation, guard evaluation, failure handling, plan traces, and human-review suspension. 

But the public `POST /api/v1/agent-runs` path does not use that as the default orchestration kernel. It still runs a PR1 fake profile and fake tool. 

That makes the repo look more advanced internally than it behaves externally.

## 2. Persistence is unsafe for audit-grade runtime

`EfCoreAgentRunRepository.SaveAsync` still removes the existing run, saves, then re-adds the whole aggregate. 

For a trace/audit/governance runtime, that is not acceptable long term. You need append-aware persistence, optimistic concurrency, and no delete/reinsert window.

## 3. Queue worker still has a scoped-lifetime risk in Postgres mode

The infrastructure DI swaps `IDurableRunQueue`, `IRunExecutionLeaseStore`, `IOutboxStore`, and related stores to scoped EF implementations in Postgres mode.  But `RunQueueHostedService` still injects `IDurableRunQueue` and `IRunExecutionLeaseStore` directly into the singleton hosted service constructor. 

It does create a scope for `StartAgentRunHandler`, but not for the queue and lease stores. That should be fixed before trusting durable background execution.

## 4. Auth is custom, partial, and not a full ASP.NET boundary

`Program.cs` configures custom actor access and a role-based decision service, but does not set up normal `AddAuthentication`, `AddAuthorization`, `UseAuthentication`, or `UseAuthorization`. 

Governance endpoints call `EndpointAuthorization.Require`, which is good.  But the primary agent-run and queue endpoints do not show the same authorization boundary.  

That means production identity exists as a project concept, but not yet as a uniform HTTP security model.

## 5. Policy scope is modeled but not enforced

This is the cleanest “must fix” item. The repo already admits it: tenant/workspace/project scopes exist on policy rules, but the adapter applies all rules globally. 

Do not add more policy sophistication until this is closed.

## 6. Review workflow is not semantically complete

`Approve` and `Reject` have meaningful state transitions. `RequestChanges` and `Escalate` mainly record a decision and leave the run in the same broad state. 

That may be acceptable for a raw audit log, but not for an operator product. You need explicit review-workflow semantics: who owns it, what state it is in, what happens next, whether execution is blocked, and how it returns to executable form.

## 7. README is still not product documentation

The README still presents the repo as an “Agentor PR1–PR40 Claude Code Package v2,” which undersells the project and makes it look like a generated planning bundle rather than a serious runtime. 

This is not cosmetic. Public-facing docs determine whether reviewers understand the project as a product or as a code-generation artifact.

---

# Recommended strategic direction

Stop adding new runtime features for a moment.

The next major phase should be **consolidation**:

> Make Agentor’s public behavior match its architecture.

That means the main route should execute real plans/tools/skills through policy, review, traces, manifests, queue, and persistence. Fake execution should remain available only as a named test/harness adapter.

---

# Comprehensive execution plan

## Phase 23 — Runtime truth and product surface alignment

**Goal:** Make the repo honest, coherent, and product-readable before deeper refactors.

### PR23.1 — Rewrite README as real product documentation

Replace the current package-style README with:

```text
# Agentor

Deterministic, observable, policy-governed agent execution runtime.

## What Agentor is
## What Agentor is not
## Current capabilities
## Current limitations
## Quickstart
## API examples
## Architecture
## Runtime model: Run → Step → Tool/Skill → Policy → Trace → Manifest
## Human review
## Integrations: Athanor, Conexus, MCP, external agents
## Development harness
## Roadmap
```

Move the old package README content into `docs/history/PR1-PR40-package.md`.

### PR23.2 — Add `docs/REPO_TRUTH.md`

This should explicitly say:

```md
Current true state:
- Public /agent-runs starts legacy fake-tool run.
- SequentialAgentPlanExecutor exists but is not yet the default public run kernel.
- Policy scopes are modeled but not enforced.
- Postgres persistence exists but aggregate save is delete/reinsert.
- Jwt mode consumes an already-authenticated principal; it does not configure bearer validation.
```

This prevents false confidence.

### PR23.3 — Add architectural decision record

Create:

```text
decisions/ADR-023-public-run-kernel-unification.md
```

Decision:

> The public run API must no longer directly encode PR1 fake-tool behavior. It must call a runtime orchestration kernel. Fake execution remains a selectable adapter/fixture.

Acceptance:

* README no longer says “Claude Code Package” as the main identity.
* Repo truth doc exists.
* Current limitations are explicit.
* Harness updated to Phase 23 / PR115 or your next numbering convention.

---

## Phase 24 — Replace fake public run with real orchestration kernel

**Goal:** Convert Agentor from “advanced internal scaffold + fake public start” into a real runtime.

### PR24.1 — Introduce `RunOrchestrationRequest`

Create a richer command model:

```csharp
public sealed record RunOrchestrationRequest(
    string AgentName,
    string Objective,
    string? TraceId,
    Guid? TenantId,
    Guid? WorkspaceId,
    Guid? ProjectId,
    Guid? KnowledgeScopeId,
    RunExecutionMode Mode,
    Guid? RecipeId,
    Guid? PlanId,
    string? ToolKey,
    string? SkillKey,
    IReadOnlyDictionary<string, JsonElement>? Input);
```

Modes:

```csharp
public enum RunExecutionMode
{
    LegacyFakeTool,
    SingleTool,
    Plan,
    Recipe,
    Skill,
    ModelCall,
    McpTool,
    ExternalAgent
}
```

### PR24.2 — Extract current fake behavior into `LegacyFakeRunExecutor`

Move PR1 behavior out of `StartAgentRunHandler`.

`StartAgentRunHandler` should become a thin compatibility shell, not the orchestration center.

### PR24.3 — Add `AgentRunOrchestrator`

Shape:

```csharp
public interface IAgentRunOrchestrator
{
    Task<AgentRun> StartAsync(RunOrchestrationRequest request, CancellationToken ct);
}
```

Responsibilities:

* Create `AgentRun`.
* Resolve requested execution mode.
* Compile plan where needed.
* Execute via `SequentialAgentPlanExecutor` or direct tool path.
* Persist through repository.
* Return run.

### PR24.4 — Make `/api/v1/agent-runs` call the orchestrator

Default behavior should become:

* If `planId` supplied: execute plan.
* If `toolKey` supplied: execute single governed tool.
* If `skillKey` supplied: execute skill-wrapped plan.
* If no mode supplied: either reject with 400 or use an explicit configured default.
* Legacy fake mode only works when `mode = LegacyFakeTool` or in test/dev config.

### PR24.5 — Tests

Required tests:

* `POST /agent-runs` with `toolKey=conexus.model-complete` invokes model gateway tool.
* `POST /agent-runs` with MCP tool invokes MCP adapter.
* `POST /agent-runs` with external-agent tool invokes external-agent adapter.
* Policy deny blocks execution before adapter call.
* Policy requires review creates pending review state.
* Legacy fake mode still works only when explicitly requested.

---

## Phase 25 — Fix queue lifetimes and background execution correctness

**Goal:** Make background execution safe with EF/Postgres mode.

### PR25.1 — Refactor `RunQueueHostedService`

Do not inject scoped queue/lease stores into the hosted-service constructor.

Current problem: EF mode registers queue and lease stores as scoped, but the hosted service receives them directly.  

Refactor to:

```csharp
public sealed class RunQueueHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    internal async Task<bool> TryProcessSingleAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        var queueStore = scope.ServiceProvider.GetRequiredService<IDurableRunQueue>();
        var leaseStore = scope.ServiceProvider.GetRequiredService<IRunExecutionLeaseStore>();
        var handlerOrOrchestrator = scope.ServiceProvider.GetRequiredService<IAgentRunOrchestrator>();

        ...
    }
}
```

### PR25.2 — Add EF-mode hosted worker test

Use SQLite or Testcontainers Postgres if available.

Acceptance:

* App boots in EF mode with hosted worker enabled.
* Queue claim → lease acquire → run execute → mark completed.
* Scoped service validation enabled in test.
* No “Cannot consume scoped service from singleton” class of failure.

### PR25.3 — Queue should call the new orchestrator

Queued work should not call fake-centered `StartAgentRunHandler`. It should call the unified orchestrator from Phase 24.

---

## Phase 26 — Enforce policy scope: close SCOPE-001

**Goal:** Make policy governance real for tenants/workspaces/projects.

### PR26.1 — Extend policy evaluation context

Add scope to:

```csharp
public sealed record PolicyEvaluationRequest(
    Guid RunId,
    Guid StepId,
    string ToolKey,
    IReadOnlyDictionary<string, string> Input,
    PolicyEvaluationContext? Context,
    AgentRunScope Scope);
```

Or resolve the run inside evaluator if necessary, but explicit scope is cleaner.

### PR26.2 — Change adapter signature

From:

```csharp
PolicyBundleRulesAdapter.ToProfileRules(bundle)
```

To:

```csharp
PolicyBundleRulesAdapter.ToProfileRules(bundle, AgentRunScope scope)
```

### PR26.3 — Implement deterministic matching

Rules apply when:

* `Global`: always.
* `Tenant`: `rule.TenantId == run.TenantId`.
* `Workspace`: tenant + workspace match.
* `Project`: tenant/workspace/project match.
* `KnowledgeScope`: knowledge scope matches.

Define precedence:

```text
Most specific rule wins:
KnowledgeScope > Project > Workspace > Tenant > Global
Deny > RequiresReview > Allow when specificity ties
```

Or use another rule, but document it.

### PR26.4 — Negative tests

Required:

* Tenant A deny does not affect Tenant B.
* Workspace-specific require-review does not affect sibling workspace.
* Project allow does not override higher-specificity deny unless explicitly designed.
* Global deny still applies when no more specific override exists.

Acceptance:

* `PolicyBundleRulesAdapter` no longer contains the SCOPE-001 limitation comment.
* Deferred-items doc marks SCOPE-001 closed.
* Audit export includes effective policy scope.

---

## Phase 27 — Persistence correctness and audit immutability

**Goal:** Remove destructive aggregate save.

### PR27.1 — Replace delete/reinsert save

Current EF save removes existing run, saves, then re-adds. 

Replace with:

* Upsert run root.
* Append trace events by ID.
* Upsert steps.
* Append tool calls / policy decisions.
* Upsert session memory snapshot.
* Store resume cursor separately or as JSON with version.
* No delete/reinsert.

### PR27.2 — Add concurrency token

Add:

```csharp
byte[] RowVersion
```

or numeric aggregate version.

Behavior:

* Concurrent update conflict returns controlled application error.
* Human review double-submit is idempotent or rejected deterministically.

### PR27.3 — Audit immutability guard

Trace events should be append-only. Tests should prove:

* Existing trace event cannot be silently rewritten.
* Re-saving same aggregate does not duplicate trace.
* Human review decision append order is stable.

### PR27.4 — Migration

Add EF migration for:

* Row version / aggregate version.
* Optional trace unique constraint.
* Optional event sequence.

---

## Phase 28 — Review workflow semantics

**Goal:** Turn review decisions into a real operator workflow.

### PR28.1 — Separate run execution state from review workflow state

Do not overload `CompletedAt` for review suspension. `EnterRequiresReview` currently sets `CompletedAt`. 

Add:

```csharp
DateTimeOffset? ReviewRequestedAt
DateTimeOffset? PausedAt
DateTimeOffset? TerminalAt
DateTimeOffset? CompletedAt // only true completion
```

Or replace with:

```csharp
EndedAt
TerminalStatus
```

### PR28.2 — Define review states

Add review workflow state, not necessarily run status:

```csharp
public enum HumanReviewWorkflowStatus
{
    Pending,
    ChangesRequested,
    Escalated,
    Approved,
    Rejected,
    Superseded
}
```

### PR28.3 — Make `RequestChanges` meaningful

Expected behavior:

* Record requested changes.
* Keep run paused.
* Add required change note.
* Optionally attach revised input / revised plan.
* Allow resubmission or operator approval after changes.

### PR28.4 — Make `Escalate` meaningful

Expected behavior:

* Mark review item escalated.
* Require higher role for final approval.
* Surface in dashboard separately.
* Preserve original reviewer and escalation reason.

### PR28.5 — Tests

* `RequestChanges` produces `ChangesRequested` review state.
* `Escalate` produces `Escalated` review state.
* Normal operator cannot approve escalated review if policy requires senior role.
* Audit export shows complete review chain.

---

## Phase 29 — Production auth boundary

**Goal:** Replace partial custom endpoint checks with a standard HTTP security layer.

### PR29.1 — Add ASP.NET authentication/authorization

`Jwt` mode currently assumes an already-authenticated principal.  That can remain true only behind a gateway, but the app should be explicit.

Add:

```csharp
builder.Services.AddAuthentication(...)
builder.Services.AddAuthorization(...)
app.UseAuthentication()
app.UseAuthorization()
```

Support deployment modes:

1. `Fake` — local/test only.
2. `Header` — trusted reverse proxy only.
3. `Jwt` — actual bearer validation or explicitly documented external-auth mode.

### PR29.2 — Protect all non-health endpoints

At minimum:

* `POST /agent-runs`: RunWrite
* `GET /agent-runs`: RunRead
* `GET /agent-runs/{id}/trace`: TraceRead
* Queue endpoints: QueueWrite / QueueRead
* Management endpoints: ManagementRead/Write
* Governance endpoints: already partly protected
* Audit export: already protected

### PR29.3 — Add authorization matrix doc

Create:

```text
docs/security/AUTHORIZATION_MATRIX.md
```

Columns:

```text
Endpoint | Permission | Roles | Data exposed | Notes
```

---

## Phase 30 — Structured tool I/O v2

**Goal:** Stop using string dictionaries as the universal execution substrate.

The current runtime heavily uses `IReadOnlyDictionary<string,string>`. That is fine for PR1-style deterministic fixtures, but weak for real MCP/model/tool execution.

### PR30.1 — Introduce `ToolPayload`

```csharp
public sealed record ToolPayload(
    JsonObject Body,
    string? SchemaId,
    string? ContentType,
    IReadOnlyDictionary<string,string> Summary);
```

### PR30.2 — Keep backward compatibility

Do not break all existing tests immediately.

Bridge:

```csharp
Dictionary<string,string> ToLegacySummary()
ToolPayload FromLegacyDictionary(...)
```

### PR30.3 — Upgrade adapter contracts

* Conexus model call input/output as JSON.
* MCP tool arguments/results as JSON.
* External agent invocation as JSON.
* Audit export contains redacted JSON body + summary.

### PR30.4 — Tests

* Nested JSON roundtrip.
* Redaction works on nested secrets.
* Trace summary remains stable.
* Legacy tools still pass.

---

## Phase 31 — Refactor large handlers/orchestrators

**Goal:** Reduce coordination debt after behavior is stabilized.

The review correctly flags that `ApplyHumanReviewDecisionHandler` is too large. The live file still owns fetching, decision construction, approval continuation, policy re-evaluation, tool execution, multi-step resume, cursor handling, and failure handling. 

Extract:

```text
HumanReviewDecisionApplicator
ReviewedToolContinuationService
PlanResumeOrchestrator
ReviewPolicyReevaluationService
ReviewTraceWriter
```

Do this after Phase 24 and Phase 28, not before, because the right boundaries become clearer once the public run kernel and review workflow are fixed.

---

# Suggested immediate PR sequence

I would do the next 10 PRs in this order:

|    PR | Name                                                        | Why first                                       |
| ----: | ----------------------------------------------------------- | ----------------------------------------------- |
| PR111 | README + repo truth + ADR for public run kernel             | Prevents false product story.                   |
| PR112 | Extract legacy fake run executor                            | Makes fake behavior explicit and removable.     |
| PR113 | Introduce `AgentRunOrchestrator`                            | Creates the central runtime seam.               |
| PR114 | Public `/agent-runs` supports real single-tool mode         | First visible product upgrade.                  |
| PR115 | Public `/agent-runs` supports plan execution                | Makes existing plan executor real product path. |
| PR116 | Queue worker resolves queue/lease/orchestrator inside scope | Fixes durable runtime safety.                   |
| PR117 | Close SCOPE-001 policy scope enforcement                    | Essential enterprise governance.                |
| PR118 | Replace EF delete/reinsert save                             | Essential audit/runtime correctness.            |
| PR119 | Review workflow states for RequestChanges/Escalate          | Operator product completeness.                  |
| PR120 | Protect all non-health endpoints                            | Production boundary.                            |

---

# Bottom line

Agentor is no longer a toy and no longer just a starter repo. The architecture has real substance: run kernel, traces, policy, human review, adapters, queue/outbox, manifests, evals, and harness discipline.

But the next work must be disciplined:

**Do not add more advanced conceptual surface.**
**Make the main runtime path real.**
**Close the safety gaps.**
**Make product behavior match the architecture.**

The single most important move is:

> Replace the fake-centered `StartAgentRunHandler` public path with a real `AgentRunOrchestrator` that can execute explicit tools, plans, skills, model calls, MCP tools, and external-agent calls through the same policy/trace/review/persistence pipeline.
