# Agentor harness - verification log

## Phase 13 + PR65.5 verification (2026-05-10)

Commands (repository root):

```
dotnet restore Agentor.sln
dotnet build Agentor.sln --no-restore
dotnet test Agentor.sln --no-build
```

Results: restore OK; build OK; test OK.

Counts: **Domain 38**, **Application 76**, **Infrastructure 59**, **Api 53** (total **226**).

Scope: Phase 13 product surface harness closeout (PR65.5): `feature-list.json` phase **13**, harnessPass **PR65.5**; extended `Phase13ProductSurfaceApiTests` (reviews pending, review decision conflict on completed run); docs `docs/api/phase13-product-surface.md`, `docs/operator/dashboard-and-inbox.md`, `docs/developer/phase13-workflows.md`, `docs/examples/phase13-workflows.md`. Phase 14 not started.

```
powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/verify-harness.ps1 -ExpectedPhase 13 -ExpectedHarnessPass PR65.5
```

Result: Harness verification passed.

## Phase 12 + PR60.6 verification (2026-05-10)

Commands (repository root):

```
dotnet restore Agentor.sln
dotnet build Agentor.sln --no-restore
dotnet test Agentor.sln --no-build
```

Results: restore OK; build OK; test OK.

Counts: **Domain 38**, **Application 76**, **Infrastructure 59**, **Api 45** (total **218**).

Scope: PR60.6 HTTP integration retry hardening — ResilientIntegrationDelegatingHandler clones HttpRequestMessage per attempt with buffered POST bodies; ResilientIntegrationDelegatingHandlerTests; InMemoryRunQueue XML note (in-memory / not broker-backed); harness feature-list.json phase **12**, harnessPass **PR60.6**; note punctuation cleanup. Phase 13 not started.

```
powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/verify-harness.ps1 -ExpectedPhase 12 -ExpectedHarnessPass PR60.6
```

Result: Harness verification passed.

## Phase 12 + PR60.5 verification (2026-05-10)

Commands (repository root):

```
dotnet restore Agentor.sln
dotnet build Agentor.sln --no-restore
dotnet test Agentor.sln --no-build
```

Results: restore OK; build OK; test OK.

Counts: **Domain 38**, **Application 76**, **Infrastructure 55**, **Api 45** (total **214**).

Scope: Phase 12 durable execution (PR56 run queue + queued API, PR57 outbox + dispatcher, PR58 leases + distributed op ledger, PR59 HTTP transport resilience registry, PR60 EF migration + Sqlite round-trips); `EfDistributedOperationLedger.TryCommitOnceAsync` clears change tracker after successful save; tests `OutboxDispatcherTests`, `Phase12EfRoundTripTests`, `TransportResilienceRegistryTests`; harness `feature-list.json` phase **12**, harnessPass **PR60.5**. Phase 13 / product operator surface not started.

```
powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/verify-harness.ps1 -ExpectedPhase 12 -ExpectedHarnessPass PR60.5
```

(`pwsh` was not on PATH; Windows PowerShell used. Result: Harness verification passed.)

## Phase 11 + PR55.5 verification (2026-05-10)

Commands (repository root):

```
dotnet restore Agentor.sln
dotnet build Agentor.sln --no-restore
dotnet test Agentor.sln --no-build
```

Results: restore OK; build OK; test OK.

Counts: **Domain 38**, **Application 69**, **Infrastructure 48**, **Api 41** (total **196**).

Scope: Phase 11 governance (PR51 identity and Athanor project resolution, PR52 ActiveProfile policy rules, PR53 human review decisions and handler, PR54 actor accessor, PR55 deterministic audit export and redaction); PR55.5 harness alignment (`feature-list.json` phase **11**, harnessPass **PR55.5**); `docs/GOVERNANCE_BOUNDARY.md`; expanded tests for project id vs profile id, DTO roundtrip, policy profile deny overrides, MCP/external deny lists, human review approve/reject/request-changes and post-approve Deny, actor header vs local fallback, audit hash and redaction, EF persistence. Deferred items remain **false** in harness: PR52-004 (PolicyBundle), PR53-005 (multi-step resume). Phase 12 / PR56 not started.


## Phase 10 + PR50.5 verification (2026-05-10)

Commands (repository root):

```
dotnet restore Agentor.sln
dotnet build Agentor.sln --no-restore
dotnet test Agentor.sln --no-build
```

Results: restore OK; build OK; test OK.

Counts: **Domain 33**, **Application 66**, **Infrastructure 46**, **Api 41** (total **186**).

Scope: `Agentor:Integrations` Fake/Http/Disabled; HTTP adapters (Athanor, Conexus, MCP, ExternalAgents); `GET /health` liveness; `GET /ready` + `GET /api/v1/integrations/status`; `IntegrationSurfaceService` HTTP probes require success status codes; Disabled adapters surface `detail: "disabled"`; tests in `IntegrationEndpointsTests`, `IntegrationSurfaceServiceTests`, HTTP client stub tests; `feature-list.json` phase **10**, harnessPass **PR50.5**. No Phase 11 identity/governance code in this pass.


## PR25.5 verification (2026-05-10)

Commands (repository root, Agentor.sln):

```
dotnet restore Agentor.sln
dotnet build Agentor.sln --no-restore
dotnet test Agentor.sln --no-build
```

Results: restore OK; build OK; test OK.

Counts: Domain 23, Application 52, Infrastructure 17, Api 34 (total 126).

Scope: itemized feature-list.json; Athanor API/application tests for 409/404/400 paths; ATHANOR_INTEGRATION_BOUNDARY.md (implemented fake port; ProfileId-as-projectId harness note). No Conexus, no real Athanor HTTP, no canonization APIs.

## PR completion note - PR25.5

feature-list.json: two items remain passes=false (PR23-API-003, PR24-API-003) for deferred public-API 2xx success paths until a running-run fixture or plan integration.

Policy: PolicyDecisionOutcome.RequiresReview remains distinct from Deny (unchanged).
## Phase 6 verification — PR26–PR30 (2026-05-10)

Commands (repository root):

```
dotnet restore Agentor.sln
dotnet build Agentor.sln --no-restore
dotnet test Agentor.sln --no-build
```

Results: restore OK; build OK; test OK.

Counts: Domain 23, Application 54, Infrastructure 21, Api 34 (total 132).

Scope: Conexus application port `IModelGatewayClient`; Contracts `ModelCallRequestDto`/`ModelCallResultDto`; `FakeModelGatewayClient` + `ModelGatewayToolExecutor`; tool key `conexus.model-complete`; prompt/model profile refs; runtime declared budget caps (`declaredCostUnits`, `declaredLatencyMs`); run manifest **v1.1** with aggregated Conexus model-call telemetry on `RunManifest`/`RunManifestDto`. No real Conexus HTTP; no provider SDKs in Domain/Application.

### PR completion notes (Phase 6 harness batch)

- **PR26** — Port + fake gateway registered in DI; Infrastructure tests for fake client.
- **PR27** — Model-call tool wired in `ToolRegistry.CreateDefault`; executor integration tests.
- **PR28** — Profile ref fields on DTOs and optional tool input/output keys `promptProfileRef` / `modelProfileRef`.
- **PR29** — `RuntimePolicyOptions.MaxDeclaredModelCallCostUnits` / `MaxDeclaredModelCallLatencyMs`; deny reason codes `BUDGET_DECLARED_COST` / `BUDGET_DECLARED_LATENCY`.
- **PR30** — Manifest version **1.1**; hash inputs include telemetry rollup; API contract test expects `1.1` and `ModelCallCount` baseline on default run.

### Encoding note

Several edits used UTF-8-safe rewrites (PowerShell `WriteAllText` / Python) where tooling wrote UTF-16 to `.cs` sources; final tree builds cleanly under UTF-8.

## PR30.5 verification -- Conexus boundary and budget hardening (2026-05-10)

Commands (repository root):

`
dotnet restore Agentor.sln
dotnet build Agentor.sln --no-restore
dotnet test Agentor.sln --no-build
`

Results: restore OK; build OK; test OK.

Counts: Domain 24, Application 56, Infrastructure 21, Api 34 (total 135).

Scope: `RunManifestModelTelemetry` + `ModelCallTelemetryAggregator` (Application) remove Conexus/tool-key coupling from Domain `RunManifest`; docs + `RuntimePolicyOptions` clarify **declared** pre-execution budget gating (optional `declaredCostUnits` / `declaredLatencyMs`); tests for missing declared keys when caps set; tests for non-zero manifest v1.1 aggregates from successful `conexus.model-complete`. No real Conexus HTTP; PR31 / Skills not started.

### Completion notes

- **Boundary**: Domain manifest hashes generic telemetry inputs; gateway-shaped JSON parsing stays in Application.
- **Budget**: Caps apply only when corresponding declared fields are present on the tool input.



## Phase 7 verification (2026-05-10)

Commands (repo root):

`
dotnet restore Agentor.sln
dotnet build Agentor.sln
dotnet test Agentor.sln
`

Results: restore OK; build OK; test OK (all projects).

Scope: PR31 SkillPackage + procedure steps; PR32 RecipeStepKind.Skill, ISkillPackageCatalog, InMemorySkillPackageCatalog, SequentialAgentPlanExecutor skill path + TraceEventKind skill kinds; PR33 SessionMemoryBudget/TryWriteSessionMemory, trace kinds, plan BuildInput session: keys, EF agent_runs.session_memory_json migration + snapshot; PR34 RunEvaluationHarness; PR35 RunQualityGateEvaluator.

PR completion notes: PR31-PR35 landed as one integrated pass with per-PR acceptance items in feature-list.json.

## Phase 7 hardening - PR35.5 (2026-05-10)

Commands (repo root):

```
dotnet restore Agentor.sln
dotnet build Agentor.sln --no-restore
dotnet test Agentor.sln --no-build
```

Scope: expanded feature-list acceptance for PR31-PR35 + PR35.5; RunEvaluationHarness fixture test; RunQualityGateEvaluator codes and plan warning; skill trace audit assertions; SESSION_MEMORY_BOUNDARY.md; PlanInputBuilder extraction.

Result: restore/build/test all succeeded (Agentor.sln).

## Phase 8 verification — PR36 (2026-05-10)

Commands (repo root):

```
dotnet restore Agentor.sln
dotnet build Agentor.sln --no-restore
dotnet test Agentor.sln --no-build
```

Results: restore OK; build OK; test OK.

Scope: `IMcpRegistryClient` + MCP descriptor records; `FakeMcpRegistryClient` (demo-server, echo/stats); DI registration; `docs/MCP_BOUNDARY.md`; `FakeMcpRegistryClientTests`. No ToolRegistry binding (PR37). No real MCP transport.

### PR completion note — PR36

MCP is an Application/Infrastructure adapter port with a deterministic fake; execution remains policy-governed once tools are registered in PR37.

## Phase 8 verification — PR37 (2026-05-10)

Commands (repo root):

```
dotnet restore Agentor.sln
dotnet build Agentor.sln --no-restore
dotnet test Agentor.sln --no-build
```

Results: restore OK; build OK; test OK.

Scope: `McpToolKeys`; `McpToolExecutor`; `ToolRegistry.CreateDefault` registers MCP catalog; DI passes `IMcpRegistryClient`; tests updated; `ToolRegistryMcpBindingTests`.

### PR completion note — PR37

Discovered MCP tools are first-class `ToolDefinition` entries; policy and `ToolExecutionPipeline` apply unchanged.

## Phase 8 verification — PR38 (2026-05-10)

Commands (repo root):

```
dotnet restore Agentor.sln
dotnet build Agentor.sln --no-restore
dotnet test Agentor.sln --no-build
```

Results: restore OK; build OK; test OK.

Scope: `AgentorDiagnostics` (`ActivitySource` + `Meter` counter `agentor.http.server.request.count`); middleware tags `agentor.trace_id`; Development JSON console logging; `docs/OBSERVABILITY.md`; `ObservabilityTests`.

### PR completion note — PR38

In-process metrics and Activities are host-neutral; JSON logs are Development-only configuration.

## Phase 8 verification — PR39 (2026-05-10)

Commands (repo root):

```
dotnet restore Agentor.sln
dotnet build Agentor.sln --no-restore
dotnet test Agentor.sln --no-build
```

Results: restore OK; build OK; test OK.

Scope: root `Dockerfile` (SDK publish → aspnet runtime); `docker-compose.yml`; `.dockerignore`; `.github/workflows/ci.yml` (dotnet 9 restore/build/test); `scripts/smoke.ps1` for `/health` against a running instance.

### PR completion note — PR39

Container image builds only `Agentor.Api`; smoke script assumes API reachable at `-BaseUrl` (default `http://localhost:8080`).

## Phase 8 verification — PR40 (2026-05-10)

Commands (repo root): same as PR39.

Results: restore OK; build OK; test OK.

Scope: service version `0.1.0-rc.1` in config and `AgentorRuntimeOptions` default; `docs/ROADMAP.md` v0.1 / Phase 8 section; test default version updated.

### PR completion note — PR40

v0.1 release candidate is configuration- and doc-level; no new runtime services beyond Phase 8 stack.

## Phase 9 verification — PR41–PR45 (2026-05-10)

Commands (repo root):

```
dotnet restore Agentor.sln
dotnet build Agentor.sln --no-restore
dotnet test Agentor.sln --no-build
```

Results: restore OK; build OK; test OK.

Counts: Domain 33, Application 66, Infrastructure 35, Api 35 (**total 169**).

Scope: `IExternalAgentProtocolClient` + `FakeExternalAgentProtocolClient` / `FakeA2AExternalAgentClient`; Contracts external-agent + A2A-shaped DTOs; `ExternalAgentToolKeys` + discover/invoke executors + `ToolRegistry.CreateDefault`; trace kinds + `ToolExecutionPipeline` external completion; `SequentialAgentPlanExecutor` policy deny/review traces for external-agent tools; `RunManifest` **v1.2** + external telemetry aggregator + API contract updates; `RunEvaluationHarness` snapshot external counts; `RunQualityGateEvaluator` `WarnOnExternalAgentOutputUnreviewed`; eval fixtures (`evaluation-harness-one-step-tool.json` extended; `external-agent-one-call.json` schema 3); `PlanInputBuilder` coordination helper (UTF-8). **No real network transports.**

### PR completion notes (Phase 9)

- **PR41** — Application port + Contracts DTOs + generic fake + boundary doc + tests.
- **PR42** — A2A-styled fake client + DTO records; DI default adapter.
- **PR43** — Tool keys, executors, registry wiring, policy integration tests.
- **PR44** — Trace/manifest/API surfaces for external-agent audit telemetry.
- **PR45** — Evaluation harness metrics + quality gate warning + JSON fixtures + tests.
