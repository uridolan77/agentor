using System.Text.Json;
using Agentor.Application.Commands;
using Agentor.Domain.Enums;
using Agentor.Infrastructure;
using Agentor.Infrastructure.Conexus;
using Agentor.Infrastructure.Mcp;
using Agentor.Infrastructure.ExternalAgents;
using Microsoft.Extensions.Options;
using Xunit;

namespace Agentor.Application.Tests;

public sealed class EvalBaselineFixtureTests
{
    private sealed class FixtureRoot
    {
        public int SchemaVersion { get; set; }
        public FixtureCommand? Command { get; set; }
        public FixtureExpectations? Expectations { get; set; }
    }

    private sealed class FixtureCommand
    {
        public string? AgentName { get; set; }
        public string? Objective { get; set; }
        public string? TraceId { get; set; }
    }

    private sealed class FixtureExpectations
    {
        public string? Status { get; set; }
        public string? ToolKey { get; set; }
        public int MinTraceEventCount { get; set; }
    }

    [Fact]
    public async Task DeterministicBaselineFixture_MatchesHandlerOutput()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "eval", "deterministic-baseline.json");
        Assert.True(File.Exists(path), $"Missing fixture: {path}");

        var json = await File.ReadAllTextAsync(path);
        var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var root = JsonSerializer.Deserialize<FixtureRoot>(json, jsonOpts);
        Assert.NotNull(root);
        Assert.Equal(1, root!.SchemaVersion);
        Assert.NotNull(root.Command);
        Assert.NotNull(root.Expectations);

        var clock = new SystemClock();
        var repository = new InMemoryAgentRunRepository();
        var fake = new FakeToolExecutor();
        var registry = ToolRegistry.CreateDefault(fake, new FakeModelGatewayClient(), new FakeMcpRegistryClient(), new FakeA2AExternalAgentClient());
        var policy = new RuntimePolicyEvaluator(registry, clock, Microsoft.Extensions.Options.Options.Create(new RuntimePolicyOptions()));
        var handler = AgentorTestComposition.CreateStartAgentRunHandler(repository, policy, registry, clock);

        var cmd = new StartAgentRunCommand(
            root.Command!.AgentName ?? "PR1 Agent",
            root.Command.Objective ?? "",
            root.Command.TraceId);

        var run = await handler.HandleAsync(cmd, CancellationToken.None);

        Assert.Equal(Enum.Parse<AgentRunStatus>(root.Expectations!.Status!), run.Status);
        Assert.True(run.Trace.Count >= root.Expectations.MinTraceEventCount);
        var tc = run.Steps[0].ToolCalls[0];
        Assert.Equal(root.Expectations.ToolKey, tc.ToolKey);
    }
}