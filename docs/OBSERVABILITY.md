# Observability (Agentor.Api)

- **Structured logs**: Development uses the built-in JSON console formatter (`appsettings.Development.json`) with scopes enabled. Operator-safe structured events and redaction helpers live under `Agentor.Application.Observability` (see `docs/operator/observability.md`).
- **Trace correlation**: `X-Agentor-Trace-Id` remains the primary request correlation id; `RequestTracingMiddleware` also starts an `Activity` (`Agentor.Api` source) with tag `agentor.trace_id`, pushes `AgentorCorrelationContext` for the request, and integration HTTP clients forward the same header. `POST /api/v1/agent-runs` responses also expose `X-Agentor-Run-Trace-Id` for the persisted run trace id.
- **Metrics**: `agentor.http.server.request.count` is a `System.Diagnostics.Metrics` counter on meter `Agentor.Api`, incremented once per HTTP request in the tracing middleware. Runtime counters/gauges (runs, policy, tools, queue depth, outbox pending, integration errors) use meter **`Agentor.Runtime`**. Exporters are host-specific and out of scope for this PR.

See **`docs/operator/observability.md`** for the full operator-facing observability guide and diagnostics bundle.
