# Session handoff — Phase 29 PR120

## Completed

- **ASP.NET layer**: **`AddAuthentication`** with **`Agentor.Fake`**, **`Agentor.Header`**, optional **`JwtBearer`**, optional **`Agentor.JwtUnvalidated`**; **`AddAuthorization`** policy **`Agentor.Authenticated`** (**`RequireAuthenticatedUser`**); pipeline **`UseAuthentication`/`UseAuthorization`** before **`MapGroup("/api/v1")`** with **`RequireAuthorization`**.
- **Permissions**: Extended **`AgentorPermission`** with run/queue/management/trace surface; **`RoleBasedAuthorizationDecisionService`** grants **`Service`** read-only **`RunRead`/`TraceRead`/`QueueRead`/`ManagementRead`** (still denies **`OpsRead`**, writes, governance write).
- **HTTP gates**: **`AgentRunEndpoints`**, **`RunQueueEndpoints`**, **`AthanorEndpoints`**, **`Phase13ProductEndpoints`** call **`EndpointAuthorization.Require`**; **`GET /ready`** and **`GET /api/v1/integrations/status`** require authenticated principal; integrations status also **`OpsRead`**.
- **Jwt configuration**: **`JwtAuthority`**, **`JwtAudience`**, **`JwtAcceptUnvalidatedBearerTokens`**; validator requires **`JwtAuthority`** or **`JwtAcceptUnvalidatedBearerTokens=true`** when **`Mode=Jwt`**.
- **Docs**: Rewrote **`docs/security/auth-boundary.md`**; added **`docs/security/AUTHORIZATION_MATRIX.md`**; updated **`docs/REPO_TRUTH.md`** authentication section.
- **Tests**: **`Phase29WebAuthenticationApiTests`** (Header 401/200); **`AgentorAuthOptionsValidatorTests`** (Jwt authority rules); **`RoleBasedAuthorizationDecisionServiceTests`** (service run/management matrix); **`HeaderOrFakeActorAccessorTests`** uses **`LocalDevelopmentFakeActorId`**.

## Verification

- `dotnet restore Agentor.sln` succeeded
- `dotnet build Agentor.sln --no-restore` succeeded
- `dotnet test Agentor.sln --no-build` succeeded (**456 passed, 0 failed**)
- `powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/verify-harness.ps1 -ExpectedPhase 29 -ExpectedHarnessPass PR120` succeeded
- `powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/verify-repo-clean.ps1` succeeded

## What is next

- **Phase 30** — structured tool I/O v2 (`ToolPayload`, bridges), per planning doc — **not started**.

## What was explicitly not started

- **Phase 30+** (tool payload refactor, handler splits in Phase 31, etc.).

## Remaining risks / false acceptance

- **`JwtAcceptUnvalidatedBearerTokens`** is intentionally unsafe without a strictly trusted network path; operators must prefer **`JwtAuthority`** + **`AddJwtBearer`** in real deployments.
- **Header authentication** always emits **`HumanOperator`** role claims; **`HumanGovernanceApprover`** still requires JWT (or future header role support) so **`ActorRole`** matches governance escalation semantics.
