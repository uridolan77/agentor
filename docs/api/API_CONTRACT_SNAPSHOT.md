# API contract snapshot (Phase 36 / PR145)

This folder holds **checked-in** contract artifacts so HTTP and JSON surface changes stay explicit in review.

## OpenAPI

| Artifact | Purpose |
|----------|---------|
| [`openapi-v1.snapshot.json`](./openapi-v1.snapshot.json) | Snapshot of `GET /openapi/v1.json` from the **Test** host (same shape as Development). |

**Drift test**: `OpenApiContractSnapshotTests` in `tests/Agentor.Api.Tests/OpenApiContractSnapshotTests.cs` compares the live document to the snapshot using canonical JSON (sorted object keys).

**Refresh after intentional API changes** (from repo root):

```powershell
$env:AGENTOR_UPDATE_OPENAPI_SNAPSHOT = "1"
dotnet test Agentor.sln --filter "FullyQualifiedName~OpenApiContractSnapshotTests"
Remove-Item Env:AGENTOR_UPDATE_OPENAPI_SNAPSHOT
```

**Gating**: In **Production**, `/openapi/v1.json` is mapped only when `Agentor:OpenApi:Enabled` is **true** (see `Program.cs`, `docs/security/auth-boundary.md`, and `OpenApiExposureApiTests`). Services still register OpenAPI generation; only the route is conditional.

## Route permissions

The authoritative route → permission → role matrix is **[`docs/security/AUTHORIZATION_MATRIX.md`](../security/AUTHORIZATION_MATRIX.md)**.

## DTO JSON compatibility

Contract DTO round-trips and JSON shape regressions are covered by **`tests/Agentor.Contracts.Tests/ContractDtoCompatibilityTests.cs`**, with versioning notes in **[`docs/developer/CONTRACT_VERSIONING.md`](../developer/CONTRACT_VERSIONING.md)**.
