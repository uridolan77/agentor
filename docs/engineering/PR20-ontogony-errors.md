# PR20 — Ontogony.Errors (Agentor.Api)

Agentor.Api replaces bespoke **`ExceptionHandlingMiddleware`** with **`AddOntogonyErrors`** + **`UseOntogonyExceptionHandling()`** from **`Ontogony.Errors`**, preserving the public JSON shape clients already expect.

## JSON shape

Configured in `Program.cs` via **`OntogonyExceptionMappingOptions`**:

- **`error`** — machine-oriented code (`ErrorCodeJsonKey`).
- **`errors`** — structured details when a mapping supplies them (`DetailsJsonKey`).
- **`message`**, **`traceId`** — unchanged semantics from Ontogony defaults.
- **`instance`** omitted (`IncludeInstanceInJson = false`).
- Unmapped 500 responses use code **`AgentorUnhandledError`** (`UnhandledErrorCode`).

## Exception mappings

Same HTTP semantics as the previous middleware, now expressed as `options.Map<T>(...)`:

- **`AgentRunPersistenceConcurrencyException`** → 409, code `AgentRunPersistenceConcurrency`, include exception message.
- **`AgentRunTraceImmutabilityException`** → 400, code `AgentRunTraceImmutability`, include exception message.
- **`RunOrchestrationValidationException`** → 400, code `RunOrchestrationValidationError`, public message for routing invalidity, **`detailsFactory`** exposes the **`Errors`** list under the `errors` key.
- **`RunOrchestrationNotFoundException`** → 404, **`resolveErrorCode`** = `ReasonCode`, **`resolvePublicMessage`** = exception `Message`, **`includeExceptionMessage: true`** so the resolved message is emitted on the wire.

## Pipeline order

**`UseOntogonyRequestTracing()`** then **`UseOntogonyExceptionHandling()`** — see [PR19 — Ontogony observability + HTTP correlation](PR19-ontogony-observability-http.md) and `ontogony-platform/docs/adoption/observability-error-ordering.md`.

## Reference

Platform adoption guide: `ontogony-platform/docs/adoption/error-middleware-adoption.md`.  
Migration note for new options: `ontogony-platform/docs/migrations/2026-05-11-pr20-ontogony-errors-json-and-mapping.md`.
