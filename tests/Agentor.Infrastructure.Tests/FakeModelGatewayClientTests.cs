using Agentor.Application.Abstractions;
using Agentor.Contracts.Conexus;
using Agentor.Domain;
using Agentor.Infrastructure.Conexus;
using Xunit;

namespace Agentor.Infrastructure.Tests;

public sealed class FakeModelGatewayClientTests
{
    [Fact]
    public async Task CompleteAsync_ReturnsDeterministicEchoAndMetrics_ForSamePrompt()
    {
        IModelGatewayClient sut = new FakeModelGatewayClient();
        var req = ModelCallRequestDto.FromLegacy("Hello Phase 6.", "test-model");

        var r1 = await sut.CompleteAsync(req, CancellationToken.None);
        var r2 = await sut.CompleteAsync(req, CancellationToken.None);

        var f1 = r1.Payload.ToPolicyEvaluationDictionary();
        var f2 = r2.Payload.ToPolicyEvaluationDictionary();

        Assert.Equal(f1["completionText"], f2["completionText"]);
        Assert.Equal(f1["promptTokens"], f2["promptTokens"]);
        Assert.Equal(f1["completionTokens"], f2["completionTokens"]);
        Assert.Equal(f1["estimatedCostUnits"], f2["estimatedCostUnits"]);
        Assert.Equal(f1["latencyMs"], f2["latencyMs"]);
        Assert.Equal(FakeModelGatewayClient.FakeProviderName, f1["providerName"]);
        Assert.Equal("test-model", f1["modelId"]);
        Assert.Contains("Hello Phase 6.", f1["completionText"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompleteAsync_UsesFallbackModel_WhenModelIdBlank()
    {
        IModelGatewayClient sut = new FakeModelGatewayClient();
        var result = await sut.CompleteAsync(ModelCallRequestDto.FromLegacy("x", "  "), CancellationToken.None);

        Assert.Equal("fake-model", result.Payload.ToPolicyEvaluationDictionary()["modelId"]);
    }
}
