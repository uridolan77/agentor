using Agentor.Application;
using Agentor.Application.Abstractions;
using Agentor.Domain;
using Agentor.Infrastructure.Conexus;
using Xunit;

namespace Agentor.Infrastructure.Tests;

public sealed class ModelGatewayToolExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_InvokesGateway_AndReturnsOutputFields()
    {
        var gateway = new FakeModelGatewayClient();
        IToolExecutor sut = new ModelGatewayToolExecutor(gateway);
        var run = AgentRun.Start(Guid.NewGuid(), "a", "o", "t", DateTimeOffset.UtcNow);
        var step = run.StartStep("s", DateTimeOffset.UtcNow);
        var input = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["prompt"] = "Hello from tool."
        };
        var request = new ToolExecutionRequest(run.Id, step.Id, WellKnownToolKeys.ConexusModelComplete, ToolPayload.FromLegacyDictionary(input));
        var result = await sut.ExecuteAsync(request, CancellationToken.None);
        Assert.True(result.Success);
        var flat = result.Output!.ToPolicyEvaluationDictionary();
        Assert.Equal(FakeModelGatewayClient.FakeProviderName, flat["providerName"]);
        Assert.Contains("Hello from tool.", flat["completionText"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_EchoesProfileRefs_InOutput()
    {
        var gateway = new FakeModelGatewayClient();
        IToolExecutor sut = new ModelGatewayToolExecutor(gateway);
        var run = AgentRun.Start(Guid.NewGuid(), "a", "o", "t", DateTimeOffset.UtcNow);
        var step = run.StartStep("s", DateTimeOffset.UtcNow);
        var input = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["prompt"] = "Hi.",
            ["promptProfileRef"] = "profiles/prompt/phase6",
            ["modelProfileRef"] = "profiles/model/staging"
        };
        var request = new ToolExecutionRequest(run.Id, step.Id, WellKnownToolKeys.ConexusModelComplete, ToolPayload.FromLegacyDictionary(input));
        var result = await sut.ExecuteAsync(request, CancellationToken.None);
        Assert.True(result.Success);
        var flat = result.Output!.ToPolicyEvaluationDictionary();
        Assert.Equal("profiles/prompt/phase6", flat["promptProfileRef"]);
        Assert.Equal("profiles/model/staging", flat["modelProfileRef"]);
    }
}
