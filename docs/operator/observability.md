# Observability and operator readiness

This document describes how Agentor exposes **logs**, **metrics**, **trace correlation**, and the **diagnostics bundle** in a way that stays safe for operators: no tool payload bodies, no raw prompts, no upstream response bodies, no tokens, and no connection strings.

## Logs

Structured logging uses stable **event names** (see `Agentor.Application.Observability.AgentorEventIds`) such as:

- `run.started`, `run.completed`, `run.failed`, `run.requires_review`
- `policy.allowed`, `policy.denied`, `policy.requires_review`
- `tool.started`, `tool.completed`, `tool.failed`
- `queue.claimed`, `queue.completed`, `queue.failed`
- `outbox.dispatch.started`, `outbox.dispatch.completed`, `outbox.dispatch.failed`
- `integration.error`

Log scopes include **run id**, **run trace id**, and **request correlation id** where available (`AgentorLogFields`, `SafeLogContext`). Free-form exception and integration text is passed through `ObservabilityRedaction` before logging.

## Metrics

Runtime counters and gauges use the **`Agentor.Runtime`** meter (`Agentor.Infrastructure.Observability.AgentorRuntimeMetricsRecorder`), including:

- `agentor.runs.*`, `agentor.policy.*`, `agentor.tools.*`
- `agentor.queue.depth`, `agentor.outbox.pending` (observable gauges)
- `agentor.integrations.errors`

**Allowed dimensions** are limited to `toolKey`, `policyEffect`, `integrationName`, and coarse `status`/`outcome` style tags — never objectives, prompts, or payload bodies.

HTTP request volume for the HTTP host is recorded on the **`Ontogony.Platform`** meter as **`ontogony.http.server.request.count`** (tag `service` = `Agentor.Api`) via `UseOntogonyRequestTracing` (see `docs/engineering/PR19-ontogony-observability-http.md`). Other **`Agentor.Runtime`** meters listed above are unchanged.

## Trace correlation

- Every HTTP response includes **`X-Ontogony-Trace-Id`** (canonical request correlation id). Legacy **`X-Agentor-Trace-Id`** is not echoed on responses unless **`EchoLegacyHeaders`** is enabled in `AddOntogonyObservability`.
- Successful **`POST /api/v1/agent-runs`** responses also include **`X-Agentor-Run-Trace-Id`** (the run’s persisted trace id / correlation to domain traces).
- The same request id is available from `AgentorCorrelationContext` (backed by `OntogonyCorrelationContext`) for the lifetime of the request. Outbound integration HTTP calls receive **`X-Ontogony-Trace-Id`** (and related correlation headers) from `Ontogony.Http.CorrelationHeadersDelegatingHandler`, plus **`X-Agentor-Trace-Id`** when absent (`AgentorLegacyTraceHeaderHandler`).
- Integration HTTP failures from `IntegrationHttpError.ThrowIfUnsuccessfulAsync` append a **`CorrelationId=`** suffix when a correlation id is present (still redacted/truncated for bodies).

## Diagnostics bundle

`GET /api/v1/ops/diagnostics-report` (requires **OpsRead**) returns:

- Default: **`application/json`** redacted snapshot (`schema: agentor.diagnostics.v1`).
- `?format=markdown`: **`text/markdown`** with the same facts embedded in a fenced JSON block.

### Safe fields (included)

Runtime version, environment name, auth mode flags (not secrets), OpenAPI document exposure flag, persistence **mode** and whether a connection string is configured (never the string), worker toggles, approximate queue/outbox counts, failed and requires-review run totals, integration readiness summary (modes + ready + short detail), active policy profile/bundle metadata (ids and version, not rule bodies), and an explicit note that on-disk evaluation fixtures are not scanned at runtime.

### Forbidden fields (never included)

Tool payload bodies, raw audit packets, raw stack traces, tokens, connection string values, raw HTTP headers, and full upstream error bodies.

### Proof boundaries

Counts and integration rows are **point-in-time** snapshots from the same repositories and integration surface used elsewhere in the API. Use alongside `/api/v1/integrations/status`, `/api/v1/ops/queue`, and `/api/v1/ops/outbox` when triaging incidents.

## How to use this during incidents

1. Capture **`X-Ontogony-Trace-Id`** from the user or client response and correlate with structured logs for that request.
2. Pull **`GET /api/v1/ops/diagnostics-report?format=markdown`** for a shareable, redacted snapshot (paste into tickets without secrets).
3. Cross-check integration rows with **`GET /api/v1/integrations/status`** and queue/outbox ops endpoints.
4. If a run id is known, use **`GET /api/v1/agent-runs/{id}`** and **`/trace`** for domain-level detail (subject to existing export/redaction rules).

## Related documentation

- `docs/OBSERVABILITY.md` — API host Activity + HTTP counter overview.
- `docs/security/SECURITY_RELEASE_CHECKLIST.md` — release posture including OpenAPI and auth.
- `docs/operator/release-smoke.md` — scripted smoke checks after deploy.
