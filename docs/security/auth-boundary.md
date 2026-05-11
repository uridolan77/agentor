# Auth boundary (Phase 19 + Phase 29)

This document defines the production identity and authorization boundary: **ASP.NET authentication** (who is signed in) plus **Agentor permissions** (what they may do via `ICurrentActorAccessor` + `IAuthorizationDecisionService`).

See also: **[AUTHORIZATION_MATRIX.md](./AUTHORIZATION_MATRIX.md)** (route → permission → roles) and **[SECURITY_RELEASE_CHECKLIST.md](./SECURITY_RELEASE_CHECKLIST.md)** (Phase 36 RC security consolidation) and **[v1-security-review.md](./v1-security-review.md)** (Phase 38 evidence and residual risks).

## Goals

- Keep actor resolution behind `ICurrentActorAccessor`.
- Keep authorization decisions behind `IAuthorizationDecisionService`.
- Use standard **`UseAuthentication` / `UseAuthorization`** so deployers can reason about HTTP 401 vs 403 consistently.
- Support deployable auth posture without coupling to a specific identity provider SDK.

## ASP.NET authentication layer (Phase 29)

- **`AddAuthentication`** registers schemes for the configured **`Agentor:Auth:Mode`**:
  - **Fake** — `Agentor.Fake` handler issues a fixed development principal (stable actor id + `HumanOperator` role claim).
  - **Header** — `Agentor.Header` handler requires a valid GUID in **`HeaderActorIdHeaderName`** and issues a principal with **`HumanOperator`** role claim. **`HumanGovernanceApprover`** is **not** representable in Header mode today; use **JWT** with an appropriate role claim (or a future header-role extension).
  - **Jwt** — when **`JwtAuthority`** is set, **`JwtBearerDefaults.AuthenticationScheme`** validates access tokens against that OIDC authority. When **`JwtAcceptUnvalidatedBearerTokens=true`** (trusted path / dev only), **`Agentor.JwtUnvalidated`** parses bearer JWTs **without signature validation** to populate `HttpContext.User`. Outside Development/Test, that combination requires **`JwtAllowUnvalidatedTokensOutsideDevelopment=true`** or startup validation fails.
- **`/openapi/v1.json`** is exposed in **Development**/**Test**/**Testing** by default; in **Production** enable **`Agentor:OpenApi:Enabled`** explicitly (route mapping runs after full configuration merge).
- **`/api/v1/*`** is grouped with **`RequireAuthorization(Agentor.Authenticated)`** so anonymous callers receive **401** before route handlers run.
- **`GET /health`** remains anonymous (liveness).
- **`GET /ready`** and **`GET /api/v1/integrations/status`** require an authenticated principal; integrations status additionally requires **`AgentorPermission.OpsRead`** at the Agentor layer.

## Auth modes

Configured under `Agentor:Auth`:

- `Mode = Fake | Header | Jwt`
- `AllowFakeOutsideDevelopment` (default `false`)
- `HeaderActorIdHeaderName` (default `X-Agentor-Actor-Id`)
- `JwtActorIdClaimTypes` (default: `nameidentifier`, `sub`, `oid`)
- `JwtDisplayNameClaimTypes` (default: `name`, `preferred_username`)
- `JwtRoleClaimType` (default: `role`)
- `JwtAuthority` (optional) — when set in **Jwt** mode, enables in-process **JWT bearer validation**.
- `JwtAudience` (optional) — passed to JWT bearer when `JwtAuthority` is set.
- `JwtAcceptUnvalidatedBearerTokens` (default `false`) — when **Jwt** mode is used **without** `JwtAuthority`, must be `true` to register the unvalidated bearer scheme (gateway / lab only). **Production-like hosts** additionally require `JwtAllowUnvalidatedTokensOutsideDevelopment=true`.
- `JwtAllowUnvalidatedTokensOutsideDevelopment` (default `false`) — explicit escape hatch when **must** run **`JwtAcceptUnvalidatedBearerTokens`** outside Development/Test (dangerous).

### Fake mode

- Intended for local/test only.
- Startup validation fails outside Development/Test unless `AllowFakeOutsideDevelopment=true` is explicitly set.

### Header mode

- Requires a valid GUID actor id in the configured header for **both** ASP.NET authentication **and** `ICurrentActorAccessor` (same header).
- Missing/invalid header → **401** from authentication (no route handler).

### Jwt mode

- **Startup**: `JwtAuthority` **or** `JwtAcceptUnvalidatedBearerTokens=true` is required (validated by `AgentorAuthOptionsValidator`). If **`JwtAcceptUnvalidatedBearerTokens`** is used **without** **`JwtAuthority`** in **Production**/**Staging**, validation fails unless **`JwtAllowUnvalidatedTokensOutsideDevelopment=true`**.
- Actor id is read from configured claim types on `HttpContext.User` and must parse to a non-empty GUID.
- Display name and role claim mappings are configurable.
- Missing or unrecognized role claim causes actor resolution to fail (**401** on protected endpoints after authentication succeeds).

## Permission model

Permissions are modeled as `AgentorPermission` (including run/queue/management/trace surface):

- `GovernanceReviewWrite`, `GovernanceReviewRead`
- `PolicyBundleWrite`, `PolicyBundleRead`
- `AuditRead`, `OpsRead`
- `RunWrite`, `RunRead`, `TraceRead`, `QueueWrite`, `QueueRead`, `ManagementRead`, `ManagementWrite`

Default role mapping (`RoleBasedAuthorizationDecisionService`):

- `System`: all permissions
- `HumanOperator` / `HumanGovernanceApprover`: all permissions
- `Service`: `PolicyBundleRead`, `AuditRead`, `GovernanceReviewRead`, `RunRead`, `TraceRead`, `QueueRead`, `ManagementRead` — explicitly **not** `OpsRead`, `RunWrite`, `QueueWrite`, `ManagementWrite`, `GovernanceReviewWrite`, `PolicyBundleWrite`

## Endpoint enforcement

`EndpointAuthorization.Require(...)` enforces permissions and returns:

- `401 Unauthorized` when actor resolution fails for current mode.
- `403 Forbidden` when actor role lacks required permission.

The full route matrix lives in **[AUTHORIZATION_MATRIX.md](./AUTHORIZATION_MATRIX.md)**.

## Readiness probe (`GET /ready`)

The route is annotated with **`RequireAuthorization(Agentor.Authenticated)`** alongside other system routes. Deployers should confirm that their ingress and health-check clients match the chosen **Auth** mode (for example, **Header** mode health checks must send the configured actor id header when probes hit Agentor directly). Phase 38 matrix sampling for unauthenticated **Header** mode focused on `/api/v1/*` operational routes; see **`docs/security/v1-security-review.md`**.

## Scope note (SCOPE-001)

Auth does not implement Tenant/Workspace/Project policy-scope filtering for HTTP actors beyond what policy evaluation already does for runs. It establishes the actor/authorization boundary so future scope enforcement can consume trusted run identity context in the policy evaluation path.
