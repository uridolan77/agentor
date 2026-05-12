# PR19 — Ontogony observability + HTTP correlation (pilot)

Agentor.Api now uses **`Ontogony.Observability`** request tracing (`UseOntogonyRequestTracing`) instead of the bespoke `RequestTracingMiddleware`.

- **Canonical response header:** `X-Ontogony-Trace-Id` (`OntogonyEventHeaders.TraceId`).
- **Legacy response echo:** Off by default (`EchoLegacyHeaders = false`). Set `EchoLegacyHeaders = true` in `AddOntogonyObservability` only while external clients still require `X-Agentor-Trace-Id` (and related aliases) on responses.
- **Inbound:** `OntogonyCorrelationContext.FromHeaders` accepts `X-Agentor-Trace-Id` when the Ontogony header is absent.
- **Outbound integrations:** `Ontogony.Http.CorrelationHeadersDelegatingHandler` adds canonical correlation headers; `AgentorLegacyTraceHeaderHandler` adds `X-Agentor-Trace-Id` when missing so downstream services that still expect the legacy header remain correlated.

## Middleware order

In `Program.cs`, **`UseOntogonyRequestTracing()` runs before `UseOntogonyExceptionHandling()`** (see [PR20 — Ontogony.Errors](PR20-ontogony-errors.md)). That way trace ids and response headers are established before any exception path reads them; error responses use **`OntogonyCorrelationContext`** (canonical `X-Ontogony-Trace-Id` on the response; inbound legacy headers are still accepted when the canonical header is absent).

## Outbound `HttpClient` handler order

Handlers are registered on named integration clients in this order: **resilience → legacy trace → Ontogony correlation**. With `IHttpClientFactory`, the **last** registered handler is the **outermost** on the outbound request, so **Ontogony correlation runs first**, then **`AgentorLegacyTraceHeaderHandler`**, then resilience. That ensures canonical headers are set before the legacy alias is applied.

HTTP server request volume for the ASP.NET host is recorded on the **`Ontogony.Platform`** meter as `ontogony.http.server.request.count` (tag `service` = `Agentor.Api`), replacing the previous `agentor.http.server.request.count` increment from the removed middleware. Dashboards and alerts that referenced the old meter must be updated.

**Build layout:** `ontogony-platform` is expected as a sibling of the `agentor` repository. CI must clone both or use published NuGet packages (see `ontogony-platform/docs/adoption/`).
