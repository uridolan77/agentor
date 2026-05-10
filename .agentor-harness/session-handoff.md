# Session handoff — Phase 19 PR91-PR95

## Completed

Phase 19 — Production identity and authorization boundary.

### PR91 — Auth mode configuration
- Added `Agentor:Auth` options model (`Fake | Header | Jwt`) in `src/Agentor.Api/Security/AgentorAuthOptions.cs`.
- Added startup validator `AgentorAuthOptionsValidator` and registered `ValidateOnStart` binding in `Program.cs`.
- Enforced fail-safe default: Fake mode is blocked outside Development/Test unless `AllowFakeOutsideDevelopment=true`.

### PR92 — Authorization model
- Added `AgentorPermission`, `AuthorizationDecision`, and `IAuthorizationDecisionService` in `src/Agentor.Application/Abstractions/IAuthorizationDecisionService.cs`.
- Added `RoleBasedAuthorizationDecisionService` default role mapping in `src/Agentor.Api/Security/RoleBasedAuthorizationDecisionService.cs`.

### PR93 — JWT actor accessor
- Extended `HeaderOrFakeActorAccessor` to mode-aware behavior for Fake/Header/Jwt.
- Added configurable JWT claim mappings:
  - `JwtActorIdClaimTypes`
  - `JwtDisplayNameClaimTypes`
  - `JwtRoleClaimType`
- JWT mode now requires authenticated principal + GUID actor id from configured claim set.

### PR94 — Endpoint authorization enforcement
- Added `EndpointAuthorization.Require(...)` helper for deterministic 401/403 API responses with `ApiErrorDto`.
- Applied permission checks to:
  - `POST /api/v1/agent-runs/{runId}/human-review` (`GovernanceReviewWrite`)
  - `GET /api/v1/agent-runs/{runId}/audit-export` (`AuditRead`)
  - `GET /api/v1/policy-bundles` (`PolicyBundleRead`)
  - `GET /api/v1/policy-bundles/{id}` (`PolicyBundleRead`)
  - `POST /api/v1/policy-bundles` (`PolicyBundleWrite`)
  - `POST /api/v1/policy-profiles/{id}/activate` (`PolicyBundleWrite`)

### PR95 — Security docs and threat notes
- Added `docs/security/auth-boundary.md`.
- Added `docs/security/deployment-threat-notes.md`.
- Updated `docs/GOVERNANCE_BOUNDARY.md` to reflect Phase 19 authz behavior.
- Updated `docs/RELEASE/v1.0-RC-DEFERRED-ITEMS.md` to retain SCOPE-001 with Phase 19 seam note.

### Tests and verification
- New/updated tests include:
  - `HeaderOrFakeActorAccessorTests`
  - `AgentorAuthOptionsValidatorTests`
  - `RoleBasedAuthorizationDecisionServiceTests`
  - `EndpointAuthorizationApiTests`
- Full verification:
  - `dotnet restore Agentor.sln` succeeded
  - `dotnet build Agentor.sln --no-restore` succeeded
  - `dotnet test Agentor.sln --no-build` succeeded (**346 passed, 0 failed**)
  - `verify-harness` succeeded
  - `verify-repo-clean` succeeded

## What is next

- Phase 20 or next explicitly scheduled phase.

## What was explicitly not started

- SCOPE-001 implementation (Tenant/Workspace/Project rule-scope filtering in policy evaluation path).
- EF persistence for Phase 18 PlanResumeCursor.
- Skill-step resume support and guard re-evaluation resume hardening beyond current Phase 18 baseline.

## Remaining risks / deferred

- `SCOPE-001` remains active and deferred to v1.1.
