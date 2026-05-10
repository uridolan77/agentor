using Agentor.Application;
using Agentor.Application.Manifest;
using Agentor.Domain;
using Agentor.Domain.Enums;
using Xunit;

namespace Agentor.Application.Tests;

public sealed class ModelCallTelemetryAggregatorTests
{
    [Fact]
    public void Aggregate_SumsSuccessfulConexusModelCompleteToolOutputs()
    {
        var now = DateTimeOffset.UtcNow;
        var run = AgentRun.Start(Guid.NewGuid(), "M", "Objective", "tr-mtel", now);
        var step = run.StartStep("s1", now);
        var call = ToolCall.Start(run.Id, step.Id, WellKnownToolKeys.ConexusModelComplete, new Dictionary<string, string> { ["prompt"] = "Hi" }, now);
        call.Succeed(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["providerName"] = "fake-conexus",
                ["modelId"] = "m1",
                ["promptTokens"] = "8",
                ["completionTokens"] = "16",
                ["estimatedCostUnits"] = "0.024",
                ["latencyMs"] = "15",
                ["promptProfileRef"] = "pp/x",
                ["modelProfileRef"] = "mp/y"
            },
            now);
        step.AddToolCall(call);
        step.Complete(now);
        run.Complete(now);

        var tel = ModelCallTelemetryAggregator.Aggregate(run);
        Assert.Equal(1, tel.ModelCallCount);
        Assert.Equal(8, tel.TotalPromptTokens);
        Assert.Equal(16, tel.TotalCompletionTokens);
        Assert.Equal(0.024m, tel.TotalEstimatedCostUnits);
        Assert.Equal(15, tel.TotalLatencyMs);
        Assert.Equal("fake-conexus", tel.PrimaryProviderName);
        Assert.Equal("m1", tel.PrimaryModelId);
        Assert.Equal("pp/x", tel.PrimaryPromptProfileRef);
        Assert.Equal("mp/y", tel.PrimaryModelProfileRef);

        var manifest = RunManifest.FromRun(run, tel);
        Assert.Equal(1, manifest.ModelCallCount);
        Assert.Equal(15, manifest.TotalModelLatencyMs);
        Assert.Equal("fake-conexus", manifest.PrimaryModelProviderName);
    }
}