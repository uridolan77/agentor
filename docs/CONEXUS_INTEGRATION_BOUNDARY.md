# Conexus Integration Boundary

## Decision

Conexus remains the model gateway. Agentor does not call OpenAI, Anthropic, local models, or provider SDKs directly from Domain/Application.

## Port (PR26+)

```csharp
public interface IModelGatewayClient
{
    Task<ModelCallResultDto> CompleteAsync(ModelCallRequestDto request, CancellationToken ct);
}
```

Contracts DTOs live in `Agentor.Contracts.Conexus`. The default infrastructure registration uses `FakeModelGatewayClient` (no HTTP, no provider SDKs).

## PR1

No Conexus runtime integration in PR1.

Use only `FakeToolExecutor`.

## Declared budget gating (runtime policy)

`RuntimePolicyOptions.MaxDeclaredModelCallCostUnits` and `MaxDeclaredModelCallLatencyMs` implement **pre-execution declared intent** only.

- Policy inspects optional tool input keys `declaredCostUnits` and `declaredLatencyMs` **when present**.
- If a cap is configured and the corresponding declared value parses above the cap, the tool call is denied before execution (`BUDGET_DECLARED_COST`, `BUDGET_DECLARED_LATENCY`).
- If declared keys are **absent**, budget gates do **not** fire — this is **not** enforcement of actual post-execution spend or measured latency (those appear in tool output and manifest telemetry after the gateway returns).

Future production Conexus may add required declarations when caps are set, post-execution budget checks, or both.

## Manifest model telemetry

Model-gateway rollup for `RunManifest` v1.1 is computed in Application (`ModelCallTelemetryAggregator`) from successful `conexus.model-complete` tool outputs. Domain holds only `RunManifestModelTelemetry` aggregates on the manifest — it does not reference Conexus by name.