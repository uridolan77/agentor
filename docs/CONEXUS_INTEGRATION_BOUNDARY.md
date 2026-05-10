# Conexus Integration Boundary

## Decision

Conexus remains the model gateway. Agentor does not call OpenAI, Anthropic, local models, or provider SDKs directly from Domain/Application.

## Future port

```csharp
public interface IModelGatewayClient
{
    Task<ModelCallResultDto> CompleteAsync(ModelCallRequestDto request, CancellationToken ct);
}
```

## PR1

No Conexus runtime integration in PR1.

Use only `FakeToolExecutor`.
