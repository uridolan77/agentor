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
