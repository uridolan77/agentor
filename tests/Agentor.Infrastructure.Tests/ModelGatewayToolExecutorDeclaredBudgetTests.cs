using Agentor.Application;
using Agentor.Application.Abstractions;
using Agentor.Contracts.Conexus;
using Agentor.Infrastructure.Conexus;

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
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["prompt"] = "p",
                ["modelId"] = "m",
                ["declaredCostUnits"] = "12.5",
                ["declaredLatencyMs"] = "900",
            });

        await sut.ExecuteAsync(request, CancellationToken.None);

        Assert.NotNull(gateway.Last);
        Assert.Equal(12.5m, gateway.Last!.DeclaredCostUnits);
        Assert.Equal(900, gateway.Last.DeclaredLatencyMs);
    }

    private sealed class CapturingGateway : IModelGatewayClient
    {
        public ModelCallRequestDto? Last;

        public Task<ModelCallResultDto> CompleteAsync(ModelCallRequestDto request, CancellationToken cancellationToken)
        {
            Last = request;
            return Task.FromResult(
                new ModelCallResultDto("ok", "p", request.ModelId, 1, 1, 0m, 1, request.PromptProfileRef, request.ModelProfileRef));
        }
    }
}
