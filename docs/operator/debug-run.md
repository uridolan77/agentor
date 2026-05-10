# Operator guide — debug a run

Operators debug Agentor runs using **read-only inspection** endpoints. This guide lists the main surfaces and how they relate. It does **not** prescribe production SLOs or log retention—those are deployment-specific.

## Identity

Most inspection endpoints are readable without elevated governance permissions. Alias surfaces that export audits require **AuditRead** (see `docs/security/auth-boundary.md`).

## Core reads

| Surface | Route | Notes |
|--------|-------|-------|
| Run summary | `GET /api/v1/agent-runs/{runId}` | Objective, status, steps, trace, governance decisions. |
| Trace (raw) | `GET /api/v1/agent-runs/{runId}/trace` | Ordered `ExecutionTraceEvent` list. |
| Steps | `GET /api/v1/agent-runs/{runId}/steps` | Step + policy decision + tool call detail. |
| Tool calls | `GET /api/v1/agent-runs/{runId}/tool-calls` | Flattened tool calls across steps. |
| Manifest | `GET /api/v1/agent-runs/{runId}/manifest` | Completed-run manifest hash and aggregates. |

## Product / operator aliases (Phase 13 surface)

| Surface | Route | Notes |
|--------|-------|-------|
| Timeline v2 | `GET /api/v1/runs/{runId}/timeline` | Ordered events plus **timelineGroups** (plan steps, skill spans, policy singletons, review decisions). Indices reference `orderedEvents`. |
| Coordination view | `GET /api/v1/runs/{runId}/coordination-view` | Plan-oriented rollup when plan metadata is present in trace. |
| Audit packet | `GET /api/v1/runs/{runId}/audit-packet` | Same contract as audit export + SHA-256 header; supports `format` query variants (see `audit-export.md`). |

## Operational snapshots (ops permission)

Queue, outbox, and lease snapshots live under `GET /api/v1/ops/*` and require **OpsRead**. They are linked from `GET /api/v1/operator/dashboard`.

## Integration readiness

- `GET /ready` — deployment readiness gate.
- `GET /api/v1/integrations/status` — adapter readiness snapshot (Fake/Http/Disabled modes).

## Dashboard entry point

`GET /api/v1/operator/dashboard` returns a read-only JSON document with links and shallow counts (pending review, failed runs, integration readiness flags). It does not embed proprietary business rules—only aggregates available from existing repositories/services.
