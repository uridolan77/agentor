# Authorization matrix

This matrix maps **HTTP routes** to **`AgentorPermission`** checks and default **role** behavior (`RoleBasedAuthorizationDecisionService`). It complements [`auth-boundary.md`](./auth-boundary.md) (auth modes and `ICurrentActorAccessor`).

**Legend**

- **ASP.NET layer**: `/api/v1/*` (and selected system routes below) require an **authenticated** principal (`RequireAuthorization(Agentor.Authenticated)`).
- **Agentor layer**: `EndpointAuthorization.Require` enforces **`AgentorPermission`** using **`ICurrentActorAccessor`** + **`IAuthorizationDecisionService`**.
- **Roles**: `System` and `HumanOperator` / `HumanGovernanceApprover` are allowed all listed permissions. **`Service`** is read-oriented: see the Service column in each row (implicit: same as “allowed for Service” when marked).

| Endpoint | Permission | Human roles | Service | Data / notes |
|----------|------------|-------------|---------|----------------|
| `GET /health` | *(none)* | Public | Public | Liveness only. |
| `GET /ready` | *(ASP.NET auth only)* | Authenticated | Authenticated | Readiness; no `AgentorPermission` gate. |
| `GET /api/v1/integrations/status` | `OpsRead` | Allowed | Denied | Integration modes; no secrets in DTO. |
| `POST /api/v1/agent-runs` | `RunWrite` | Allowed | Denied | Starts runs; idempotency key supported. |
| `GET /api/v1/agent-runs` | `RunRead` | Allowed | Allowed | Paginated list. |
| `GET /api/v1/agent-runs/{id}` | `RunRead` | Allowed | Allowed | Run aggregate. |
| `GET /api/v1/agent-runs/{id}/trace` | `TraceRead` | Allowed | Allowed | Trace events. |
| `GET /api/v1/agent-runs/{id}/steps` | `TraceRead` | Allowed | Allowed | Steps. |
| `GET /api/v1/agent-runs/{id}/tool-calls` | `TraceRead` | Allowed | Allowed | Tool calls. |
| `GET /api/v1/agent-runs/{id}/manifest` | `RunRead` | Allowed | Allowed | Run manifest. |
| `POST /api/v1/agent-runs/queued` | `QueueWrite` | Allowed | Denied | Enqueue background work. |
| `GET /api/v1/agent-runs/queued/{workItemId}` | `QueueRead` | Allowed | Allowed | Queue status. |
| `GET /api/v1/agent-runs/{id}/athanor/*` (read) | `RunRead` | Allowed | Allowed | Latest snapshot / canonical lookup. |
| `POST /api/v1/agent-runs/{id}/athanor/*` (mutations) | `RunWrite` | Allowed | Denied | Evidence, candidates, review-queue. |
| `POST /api/v1/agent-runs/{id}/human-review` | `GovernanceReviewWrite` | Allowed | Denied | Human review decisions. |
| `GET /api/v1/agent-runs/{id}/audit-export` | `AuditRead` | Allowed | Allowed | Audit export JSON. |
| `GET/POST /api/v1/policy-bundles`, `POST .../policy-profiles/{id}/activate` | `PolicyBundleRead` / `PolicyBundleWrite` | Per method | Read vs write | See OpenAPI tags **Policy**. |
| `GET /api/v1/ops/queue`, `.../outbox`, `.../leases` | `OpsRead` | Allowed | Denied | Operational read models. |
| `GET /api/v1/recipes`, `GET .../{id}` | `ManagementRead` | Allowed | Allowed | Artifact store. |
| `POST /api/v1/recipes` | `ManagementWrite` | Allowed | Denied | Create recipe. |
| `GET /api/v1/plans`, `GET .../{id}` | `ManagementRead` | Allowed | Allowed | Plans. |
| `POST /api/v1/plans` | `ManagementWrite` | Allowed | Denied | Instantiate plan. |
| `GET /api/v1/skills`, `GET .../{key}/{version}` | `ManagementRead` | Allowed | Allowed | Skill catalog. |
| `POST /api/v1/skills` | `ManagementWrite` | Allowed | Denied | Register skill package. |
| `GET /api/v1/policy-profiles`, `GET .../{id}` | `ManagementRead` | Allowed | Allowed | Profile artifacts. |
| `POST /api/v1/policy-profiles` | `ManagementWrite` | Allowed | Denied | Create profile artifact. |
| `GET /api/v1/runs/{id}/timeline` | `RunRead` | Allowed | Allowed | Timeline v2. |
| `GET /api/v1/runs/{id}/coordination-view` | `RunRead` | Allowed | Allowed | Coordination view. |
| `GET /api/v1/runs/{id}/audit-packet` | `AuditRead` | Allowed | Allowed | Alias audit export. |
| `GET /api/v1/operator/dashboard` | `OpsRead` | Allowed | Denied | Operator dashboard. |
| `GET /api/v1/reviews/pending` | `GovernanceReviewRead` | Allowed | Allowed | Inbox. |
| `POST /api/v1/reviews/{id}/decisions` | `GovernanceReviewWrite` | Allowed | Denied | Review alias. |

**Header mode note**: ASP.NET authentication builds a principal with role **`HumanOperator`** from the actor id header; elevated roles such as **`HumanGovernanceApprover`** require **JWT** (or future header extensions) so claims carry the correct **`ActorRole`**.
