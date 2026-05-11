# Integration smoke (Phase 35)

Operator-facing end-to-end checks for HTTP integration adapters (Athanor knowledge state, Conexus model gateway, MCP registry, external-agent protocol). Defaults are **disabled**; no secrets belong in the repository.

## Configuration model

| Section | Purpose |
| --- | --- |
| `Agentor:IntegrationSmoke` | Per-family `Mode`: `Disabled` (default), `Fake`, or `Http`. Optional Athanor write gate. |
| `Agentor:Integrations` | When a family uses `Http`, set `Http:BaseUrl` and optional headers here (same as runtime API). |

Typed types: `IntegrationSmokeOptions`, `SmokeMode`, `SmokeTarget` in `Agentor.Infrastructure.Options`; mode merge helper `IntegrationSmokeConfigurationMerger` in `Agentor.Infrastructure.Smoke`.

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

Flags:

- `-OutputDirectory` — where `integration-smoke-report.json` and `integration-smoke-report.md` are written (default: `./artifacts/integration-smoke`).
- `-Target` — repeat to limit families (`Athanor`, `Conexus`, `Mcp`, `ExternalAgents`), matching `SmokeTarget` names.

Direct `dotnet` invocation:

```powershell
dotnet run --project tools/Agentor.IntegrationSmoke -c Release -- --output ./artifacts/integration-smoke
```

## Reports

Failures include **HTTP status codes** when the upstream threw `HttpRequestException`. Free-text details are **redacted** via `IntegrationFailureRedaction` (same rules as integration HTTP clients).

## CI

Running this pack against real services is **optional** in CI; automated coverage uses **Fake** modes in `Agentor.Infrastructure.Tests` (`IntegrationSmokeFakeRunnerTests`).
