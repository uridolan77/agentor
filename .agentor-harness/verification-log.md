# Agentor harness - verification log

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
