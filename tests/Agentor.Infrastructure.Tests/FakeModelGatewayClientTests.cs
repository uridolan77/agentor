using Agentor.Application.Abstractions;
using Agentor.Contracts.Conexus;
using Agentor.Infrastructure.Conexus;
using Xunit;

namespace Agentor.Infrastructure.Tests;

public sealed class FakeModelGatewayClientTests
{
    [Fact]
    public async Task CompleteAsync_ReturnsDeterministicEchoAndMetrics_ForSamePrompt()
    {
        IModelGatewayClient sut = new FakeModelGatewayClient();
        var req = new ModelCallRequestDto("Hello Phase 6.", "test-model");

        var r1 = await sut.CompleteAsync(req, CancellationToken.None);
        var r2 = await sut.CompleteAsync(req, CancellationToken.None);

        Assert.Equal(r1.CompletionText, r2.CompletionText);
        Assert.Equal(r1.PromptTokens, r2.PromptTokens);
        Assert.Equal(r1.CompletionTokens, r2.CompletionTokens);
        Assert.Equal(r1.EstimatedCostUnits, r2.EstimatedCostUnits);
        Assert.Equal(r1.LatencyMs, r2.LatencyMs);
        Assert.Equal(FakeModelGatewayClient.FakeProviderName, r1.ProviderName);
        Assert.Equal("test-model", r1.ModelId);
        Assert.Contains("Hello Phase 6.", r1.CompletionText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompleteAsync_UsesFallbackModel_WhenModelIdBlank()
    {
        IModelGatewayClient sut = new FakeModelGatewayClient();
        var result = await sut.CompleteAsync(new ModelCallRequestDto("x", "  "), CancellationToken.None);

        Assert.Equal("fake-model", result.ModelId);
    }
}
