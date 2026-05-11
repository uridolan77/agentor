using Agentor.Application;
using Agentor.Application.Abstractions;
using Agentor.Contracts.Conexus;
using Agentor.Domain;
using Agentor.Infrastructure.Conexus;
using Xunit;

namespace Agentor.Infrastructure.Tests;

public sealed class ModelGatewayToolExecutorDeclaredBudgetTests
{
    [Fact]
    public async Task ExecuteAsync_passes_declared_budget_fields_to_gateway()
    {
        var gateway = new CapturingGateway();
        var sut = new ModelGatewayToolExecutor(gateway);
        var request = new ToolExecutionRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            WellKnownToolKeys.ConexusModelComplete,
            ToolPayload.FromLegacyDictionary(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["prompt"] = "p",
                ["modelId"] = "m",
                ["declaredCostUnits"] = "12.5",
                ["declaredLatencyMs"] = "900",
            }));

        await sut.ExecuteAsync(request, CancellationToken.None);

        Assert.NotNull(gateway.Last);
        var captured = gateway.Last!.Payload.ToPolicyEvaluationDictionary();
        Assert.Equal("12.5", captured["declaredCostUnits"]);
        Assert.Equal("900", captured["declaredLatencyMs"]);
    }

    private sealed class CapturingGateway : IModelGatewayClient
    {
        public ModelCallRequestDto? Last;

        public Task<ModelCallResultDto> CompleteAsync(ModelCallRequestDto request, CancellationToken cancellationToken)
        {
            Last = request;
            var pm = request.Payload.ToPolicyEvaluationDictionary();
            var mid = pm.TryGetValue("modelId", out var m) ? m : "m";
            return Task.FromResult(
                ModelCallResultDto.FromLegacy("ok", "p", mid, 1, 1, 0m, 1));
        }
    }
}
