# Session handoff — Phase 21 PR101–PR105

## Completed

- **PR101 — Athanor contract tests**: `HttpKnowledgeStateClientTests` expanded for latest snapshot 404→null, evidence search (including null JSON body→empty list), candidate submit and review-queue success paths, non-2xx `HttpRequestException`, and explicit relative-path check that no `canonize` segment appears on documented routes (complements `NonCanonizationBoundaryTests` on the port).
- **PR102 — Conexus contract tests**: `HttpModelGatewayClient` now throws structured `HttpRequestException` on failure; tests cover full telemetry/profile deserialization, declared budget JSON round-trip on POST body, 503 mapping, and short `HttpClient.Timeout` → `OperationCanceledException`. `ModelCallRequestDto` / `ModelGatewayToolExecutor` pass `declaredCostUnits` / `declaredLatencyMs` through to the gateway when parseable (`ModelGatewayToolExecutorDeclaredBudgetTests`). Manifest telemetry path unchanged (`ModelCallTelemetryAggregatorTests`).
- **PR103 — MCP contract tests**: `HttpMcpRegistryClientExtraTests` for `ListToolsAsync`, invalid nominal risk→Medium, and 500 error mapping; `ToolRegistryMcpBindingTests` stable key format; `McpDescriptorDomainBoundaryTests` asserts descriptors live under `Agentor.Application`, not Domain.
- **PR104 — External-agent contract tests**: `HttpExternalAgentProtocolClient` structured errors; wire `IsNonCanonEvidence` deserialization; `ExternalAgentInvokeToolExecutor` adds `isNonCanonEvidence` to tool output; `ExternalAgentPolicyPreventsHttpInvocationTests` proves deny and requires-review block counting client (no invoke).
- **PR105 — Compatibility matrix**: `docs/integrations/compatibility-matrix.md`.

## Verification

- `dotnet restore Agentor.sln` succeeded
- `dotnet build Agentor.sln --no-restore` succeeded
- `dotnet test Agentor.sln --no-build` succeeded (**394 passed, 0 failed**)
- `powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/verify-harness.ps1 -ExpectedPhase 21 -ExpectedHarnessPass PR105` succeeded
- `powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/verify-repo-clean.ps1` succeeded

## What is next

- Phase 22 or the next explicitly scheduled planning phase.

## What was explicitly not started

- Phase 22+ product work.
- SCOPE-001 policy scope enforcement (still deferred per prior harness notes).

## Remaining risks / false acceptance

- None newly marked `passes: false` in `feature-list.json` for Phase 21 rows; all Phase 21 acceptance rows cite named tests or files.
