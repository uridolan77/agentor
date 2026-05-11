# Local deployment

Use this layout for **developer laptops** and **automated tests**. It prioritizes fast feedback over production parity.

## Goals

- Run **`Agentor.Api`** with **Fake** authentication and **in-memory** persistence (defaults in `src/Agentor.Api/appsettings.json`).
- Optionally enable **OpenAPI** in Development without extra flags.

## .NET prerequisites

- **.NET 9 SDK** (matches `global.json` / project `TargetFramework` if present).

## Configuration highlights

| Concern | Typical local value |
|--------|---------------------|
| **Persistence** | `AgentorPersistence:Mode=InMemory` (default) or SQLite for persistence experiments. |
| **Auth** | `Agentor:Auth:Mode=Fake` (default). Do not set `AllowFakeOutsideDevelopment` in real shared environments. |
| **Integrations** | `Agentor:Integrations:*:*:Mode=Fake` (default). |
| **OpenAPI** | `Agentor:OpenApi:Enabled=false` in committed defaults; Development host maps `/openapi/v1.json` without this flag. |
| **Queue worker** | `Agentor:RunWorker:Enabled=false` (default). |
| **Outbox dispatcher** | `Agentor:OutboxDispatch:Enabled=false` (default). |

## Run the API

```powershell
dotnet run --project src/Agentor.Api/Agentor.Api.csproj
```

Smoke:

```powershell
pwsh ./scripts/smoke.ps1
```

## PostgreSQL

Not required locally when using **InMemory** mode. If you want EF + Postgres locally, set `AgentorPersistence:Mode=Postgres` and a valid connection string; apply migrations per `docs/developer/MIGRATION_AND_UPGRADE.md`.

## Secrets

Avoid placing production secrets in `appsettings.Development.json` that might be committed; use user secrets or environment variables.

## See also

- `docs/deployment/staging.md` — closer to production posture.
- `docs/operator/runbook.md` — incident patterns.
