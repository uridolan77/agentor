# Release HTTP smoke (Phase 36 / PR146)

The script **`scripts/release-smoke.ps1`** performs a minimal authenticated tour of the API against a **running** `Agentor.Api` instance:

1. `GET /health`
2. `GET /ready`
3. `GET /api/v1/integrations/status`
4. `POST /api/v1/agent-runs` (minimal JSON body)
5. `GET /api/v1/agent-runs/{id}`
6. `GET /api/v1/agent-runs/{id}/trace`
7. `GET /api/v1/agent-runs/{id}/audit-export` (default canonical format)
8. `GET /api/v1/operator/dashboard`

## Usage

With **Fake** auth (typical local `dotnet run`):

```powershell
dotnet run --project src/Agentor.Api
# separate shell:
pwsh ./scripts/release-smoke.ps1 -BaseUrl http://localhost:8080
```

With **Header** auth, pass a stable actor GUID:

```powershell
pwsh ./scripts/release-smoke.ps1 -BaseUrl http://localhost:8080 -HeaderActorId "22222222-2222-4222-8222-222222222222"
```

## Machine-readable report (PR148.5)

Pass **`-OutputDirectory`** to also write **`release-smoke-report.json`** and **`release-smoke-report.md`** alongside console output:

```powershell
pwsh ./scripts/release-smoke.ps1 -BaseUrl http://localhost:8080 -OutputDirectory ./artifacts/release-smoke
```

The JSON shape lists each step (`name`, `method`, `uri`, `expectedStatus`, `actualStatus`, `ok`) plus top-level `overallOk` and the captured `runId`. This is intentionally simple and is **not** a replacement for the integration smoke pack.

## Proof boundaries

This script validates **routing, auth, and basic read surfaces** on a live host. It does **not** replace integration smoke against real Athanor/Conexus/MCP HTTP gateways; use **`scripts/run-integration-smoke.ps1`** and **`docs/operator/integration-smoke.md`** for that scope.
