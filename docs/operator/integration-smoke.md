# Integration smoke (Phase 35)

Operator-facing checks for integration adapters (Athanor knowledge state, Conexus model gateway, MCP registry, external-agent protocol). Defaults are **disabled**; no secrets belong in the repository.

## What is (and is not) proven

| Scenario | What it proves |
| --- | --- |
| **Fake** modes in CI / local tests (`IntegrationSmokeFakeRunnerTests`) | Tool wiring, adapter-mode composition, report generation, and runner logic against in-process fakes. **Not** live service behavior. |
| **Http** modes with real `BaseUrl` (operator runs the CLI) | Whatever you actually exercised against those endpoints in that run. **Not** automatic proof of production readiness unless you ran it against configured live gateways and reviewed results. |
| **HTTP adapter contract tests** (earlier phases) | Request/response shapes and client error handling for the HTTP adapters. **Not** a substitute for operator smoke against your deployment. |

**Real HTTP smoke is not proof** unless you run the tool with `Http` modes and valid endpoints/credentials in your environment and treat the emitted reports as evidence for that run only.

**Athanor candidate submit** is **write smoke**: it stays **off** unless `AllowAthanorWriteSmoke=true`. Read-only paths (snapshot, canonical, evidence) run without that flag.

## Configuration model

| Section | Purpose |
| --- | --- |
| `Agentor:IntegrationSmoke` | Per-family `Mode`: `Disabled` (default), `Fake`, or `Http`. Optional Athanor write gate. |
| `Agentor:Integrations` | When a family uses `Http`, set `Http:BaseUrl` and optional headers here (same as runtime API). |

Typed types: `IntegrationSmokeOptions`, `SmokeMode`, `SmokeTarget` in `Agentor.Infrastructure.Options`; mode merge helper `IntegrationSmokeConfigurationMerger` in `Agentor.Infrastructure.Smoke`.

### Target filtering (`--target`)

Valid names match `SmokeTarget`: `Athanor`, `Conexus`, `Mcp`, `ExternalAgents` (case-insensitive). Unknown names fail the CLI immediately (exit code **2**). If you pass one or more valid targets but every requested family is still `Disabled`, the run **fails** with a diagnostic step (`Cli` / `explicitTargetNoWork`) so you do not get a silent zero-step “OK”.

## Environment variables

Use double-underscore nesting (examples):

- `Agentor__IntegrationSmoke__Athanor__Mode=Http`
- `Agentor__Integrations__Athanor__Http__BaseUrl=https://athanor.example/`
- `Agentor__IntegrationSmoke__AllowAthanorWriteSmoke=true` (required to run Athanor **candidate submit** smoke; otherwise that step is skipped)

Optional tuning:

- `Agentor__IntegrationSmoke__McpSmokeServerId`, `Agentor__IntegrationSmoke__McpSmokeToolName`
- `Agentor__IntegrationSmoke__ExternalAgentSmokeAgentKey`, `Agentor__IntegrationSmoke__ExternalAgentSmokeCapabilityKey`
- `Agentor__IntegrationSmoke__AthanorProjectId`, `Agentor__IntegrationSmoke__AthanorCanonicalLookupKey`, `Agentor__IntegrationSmoke__AthanorEvidenceSearchQuery`

## Local / operator script

From the repository root:

```powershell
pwsh ./scripts/run-integration-smoke.ps1 -Configuration Release
```

On Windows without `pwsh`, use **Windows PowerShell** the same way CI does:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/run-integration-smoke.ps1 -Configuration Release
```

Flags:

- `-OutputDirectory` — where `integration-smoke-report.json` and `integration-smoke-report.md` are written (default: `./artifacts/integration-smoke`).
- `-Target` — repeat to limit families (`Athanor`, `Conexus`, `Mcp`, `ExternalAgents`).

Direct `dotnet` invocation:

```powershell
dotnet run --project tools/Agentor.IntegrationSmoke -c Release -- --output ./artifacts/integration-smoke
```

## Reports

Failures include **HTTP status codes** when the upstream threw `HttpRequestException`. Step details are **redacted** when written to disk (`IntegrationSmokeReportWriter` applies `IntegrationFailureRedaction` to every `Detail` field in JSON and Markdown, even if a future caller passed raw text).

## CI

Running this pack against real services is **optional** in CI; automated coverage uses **Fake** modes in `Agentor.Infrastructure.Tests` (`IntegrationSmokeFakeRunnerTests`) and does **not** substitute for operator-run HTTP smoke.
