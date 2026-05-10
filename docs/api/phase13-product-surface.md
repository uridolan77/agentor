# Phase 13 product API surface (`/api/v1`)

Phase 13 adds **management** and **operator** routes alongside the existing runtime API. All paths are under `GET`/`POST` `/api/v1/...` unless noted.

## Management (no execution on create)

| Area | Routes | Notes |
|------|--------|--------|
| Recipes | `GET/POST /recipes`, `GET /recipes/{id}` | Validates body; persists recipe definitions only. |
| Plans | `GET/POST /plans`, `GET /plans/{id}` | Instantiates a plan from a stored recipe; does **not** enqueue or run it. |
| Skills | `GET/POST /skills`, `GET /skills/{key}/{version}` | Registers skill package metadata; no skill execution here. |
| Policy profiles | `GET/POST /policy-profiles`, `GET /policy-profiles/{id}` | Declarative policy artifacts; runtime wiring is separate. |

Validation failures return `400` with `ApiErrorDto` (machine-oriented codes and messages).

## Run inspection aliases

| Route | Purpose |
|-------|---------|
| `GET /runs/{runId}/timeline` | Trace ordered by time; skill invocation span grouping. |
| `GET /runs/{runId}/coordination-view` | Plan-oriented step state derived from trace + stored plan snapshot when present. |
| `GET /runs/{runId}/audit-packet` | Same canonical JSON and `X-Agentor-Audit-Content-SHA256` as `GET /agent-runs/{runId}/audit-export`. |

## Operator and reviews

| Route | Purpose |
|-------|---------|
| `GET /operator/dashboard` | Read-only DTO: module links and counts; requires **`OpsRead`** (aligned with `/ops/*`). |
| `GET /reviews/pending` | Inbox: runs in `RequiresReview`. |
| `POST /reviews/{runId}/decisions` | Alias of `POST /agent-runs/{runId}/human-review` (governance; actor context via `X-Agentor-Actor-Id` where configured). |

## Service boundaries (non-negotiable)

- **Agentor** records runs, traces, policy decisions, tool outcomes, and manifests. It does **not** canonize knowledge.
- **Athanor** remains the canonical knowledge-state and provenance service; Agentor adapters surface evidence and candidates as **non-canon** inputs.
- **Conexus** remains the model gateway; model calls are tools routed through that boundary, not implicit chat.

See also: `docs/GOVERNANCE_BOUNDARY.md`, `docs/SERVICE_BOUNDARIES.md`, `docs/ATHANOR_INTEGRATION_BOUNDARY.md`, `docs/CONEXUS_INTEGRATION_BOUNDARY.md`.
