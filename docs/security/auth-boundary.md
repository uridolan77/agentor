# Auth Boundary (Phase 19)

This document defines the production identity and authorization boundary introduced in Phase 19.

## Goals

- Keep actor resolution behind `ICurrentActorAccessor`.
- Keep authorization decisions behind `IAuthorizationDecisionService`.
- Support deployable auth posture without coupling to a specific identity provider SDK.

## Auth modes

Configured under `Agentor:Auth`:

- `Mode = Fake | Header | Jwt`
- `AllowFakeOutsideDevelopment` (default `false`)
- `HeaderActorIdHeaderName` (default `X-Agentor-Actor-Id`)
- `JwtActorIdClaimTypes` (default: `nameidentifier`, `sub`, `oid`)
- `JwtDisplayNameClaimTypes` (default: `name`, `preferred_username`)
- `JwtRoleClaimType` (default: `role`)

### Fake mode

- Intended for local/test only.
- Startup validation fails outside Development/Test unless `AllowFakeOutsideDevelopment=true` is explicitly set.

### Header mode

- Requires a valid GUID actor id in the configured header.
- Missing/invalid header returns an unauthorized API response through endpoint authorization checks.

### Jwt mode

- Requires an authenticated principal.
- Actor id is read from configured claim types and must parse to a non-empty GUID.
- Display name and role claim mappings are configurable.
- Missing or unrecognized role claim causes actor resolution to fail (unauthorized response on protected endpoints).
- No provider-specific SDK is required.

Important runtime note:

- Current repository Jwt mode consumes an already-authenticated `HttpContext.User`.
- Repository startup does not automatically configure bearer token validation middleware (`AddAuthentication`/`AddJwtBearer`/`UseAuthentication`).
- Token validation is expected from upstream gateway/middleware unless explicitly added in a later pass.

## Permission model

Permissions are modeled as `AgentorPermission`:

- `GovernanceReviewWrite`
- `PolicyBundleWrite`
- `PolicyBundleRead`
- `AuditRead`
- `OpsRead`

Default role mapping (`RoleBasedAuthorizationDecisionService`):

- `System`: all permissions
- `HumanOperator`: all permissions
- `Service`: read-only permissions (`PolicyBundleRead`, `AuditRead`, `GovernanceReviewRead`) and explicitly not `OpsRead`

## Endpoint enforcement

`EndpointAuthorization.Require(...)` enforces permissions and returns:

- `401 Unauthorized` when actor resolution fails for current mode.
- `403 Forbidden` when actor role lacks required permission.

Current enforced endpoints include:

- `POST /api/v1/agent-runs/{runId}/human-review` -> `GovernanceReviewWrite`
- `GET /api/v1/agent-runs/{runId}/audit-export` -> `AuditRead`
- `GET /api/v1/policy-bundles` -> `PolicyBundleRead`
- `GET /api/v1/policy-bundles/{id}` -> `PolicyBundleRead`
- `POST /api/v1/policy-bundles` -> `PolicyBundleWrite`
- `POST /api/v1/policy-profiles/{id}/activate` -> `PolicyBundleWrite`
- `GET /api/v1/runs/{runId}/audit-packet` -> `AuditRead`
- `POST /api/v1/reviews/{runId}/decisions` -> `GovernanceReviewWrite`
- `GET /api/v1/reviews/pending` -> `GovernanceReviewRead`
- `GET /api/v1/ops/queue` -> `OpsRead`
- `GET /api/v1/ops/outbox` -> `OpsRead`
- `GET /api/v1/ops/leases` -> `OpsRead`

## Scope note (SCOPE-001)

Phase 19 auth does not implement Tenant/Workspace/Project policy-scope filtering.
It does establish the actor/authorization boundary needed so future scope enforcement can consume trusted run identity context in the policy evaluation path.
