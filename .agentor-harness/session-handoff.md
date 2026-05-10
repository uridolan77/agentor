# Session handoff — Phase 19 PR95.5

## Completed

PR95.5 — Phase 19 authorization hardening.

### Alias endpoint authorization parity
- Added permission checks to Phase 13 aliases in `src/Agentor.Api/Phase13ProductEndpoints.cs`:
  - `GET /api/v1/runs/{runId}/audit-packet` requires `AgentorPermission.AuditRead`.
  - `POST /api/v1/reviews/{runId}/decisions` requires `AgentorPermission.GovernanceReviewWrite`.
- Added authorization check to review inbox:
  - `GET /api/v1/reviews/pending` requires `AgentorPermission.GovernanceReviewRead`.

### JWT role hardening
- Updated `src/Agentor.Api/Security/HeaderOrFakeActorAccessor.cs`:
  - Missing Jwt role claim now throws `InvalidOperationException`.
  - Unrecognized Jwt role claim now throws `InvalidOperationException`.
  - Removed permissive fallback to `HumanOperator`.

### Authorization model update
- Added `GovernanceReviewRead` permission to `AgentorPermission`.
- Updated `RoleBasedAuthorizationDecisionService` so `Service` can perform read-only review inbox access (`GovernanceReviewRead`) while write actions remain denied.

### Documentation updates
- Updated `docs/security/auth-boundary.md`:
  - Alias endpoints listed as protected.
  - Jwt mode principal-consumption vs JwtBearer validation middleware distinction made explicit.
  - Strict role-claim behavior documented.
- Updated `docs/security/deployment-threat-notes.md`:
  - Added alias endpoint bypass prevention note.
  - Added Jwt role hardening note.

### Tests
- Updated `tests/Agentor.Api.Tests/EndpointAuthorizationApiTests.cs`:
  - Service actor forbidden on `POST /api/v1/reviews/{runId}/decisions`.
  - Service actor allowed on `GET /api/v1/runs/{runId}/audit-packet` under `AuditRead` policy.
  - Service actor allowed on `GET /api/v1/reviews/pending` under `GovernanceReviewRead` policy.
  - Unauthorized path coverage when actor accessor throws.
- Updated `tests/Agentor.Api.Tests/HeaderOrFakeActorAccessorTests.cs`:
  - Missing Jwt role claim throws.
  - Invalid Jwt role claim throws.
- Updated `tests/Agentor.Api.Tests/RoleBasedAuthorizationDecisionServiceTests.cs`:
  - Service actor allowed for `GovernanceReviewRead`.

## Verification

- `dotnet restore Agentor.sln` succeeded
- `dotnet build Agentor.sln --no-restore` succeeded
- `dotnet test Agentor.sln --no-build` succeeded
- `pwsh -NoProfile -ExecutionPolicy Bypass -File ./scripts/verify-harness.ps1 -ExpectedPhase 19 -ExpectedHarnessPass PR95.5` succeeded
- `pwsh -NoProfile -ExecutionPolicy Bypass -File ./scripts/verify-repo-clean.ps1` succeeded

## What is next

- Phase 20 or next explicitly scheduled phase.

## What was explicitly not started

- New Phase 20 implementation work in this pass.
- SCOPE-001 enforcement (Tenant/Workspace/Project policy-scope filtering).
- New external integrations or provider-specific identity onboarding.

## Remaining risks / deferred

- `SCOPE-001` remains active and deferred to v1.1.
