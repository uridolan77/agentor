using Agentor.Application.Commands;
using Agentor.Domain.Enums;
using Agentor.Infrastructure;
using Microsoft.Extensions.Options;
using Xunit;

namespace Agentor.Application.Tests;

public sealed class StartAgentRunHandlerTests
{
    [Fact]
    public async Task HandleAsync_CreatesCompletedRun_WithPolicyToolAndTrace()
    {
        var clock = new SystemClock();
        var repository = new InMemoryAgentRunRepository();
        var fake = new FakeToolExecutor();
        var registry = ToolRegistry.CreateDefault(fake);
        var policy = new RuntimePolicyEvaluator(registry, clock, Microsoft.Extensions.Options.Options.Create(new RuntimePolicyOptions()));
        var handler = new StartAgentRunHandler(repository, policy, registry, clock);

        var run = await handler.HandleAsync(
            new StartAgentRunCommand("PR1 Agent", "Prove the runtime kernel.", "test-trace"),
            CancellationToken.None);

        Assert.Equal(AgentRunStatus.Completed, run.Status);
        Assert.Single(run.Steps);
        Assert.Single(run.Steps[0].PolicyDecisions);
        Assert.Single(run.Steps[0].ToolCalls);
        Assert.True(run.Trace.Count >= 5);

        var saved = await repository.GetAsync(run.Id, CancellationToken.None);
        Assert.NotNull(saved);
    }

    [Fact]
    public async Task HandleAsync_WhenToolNotRegistered_FailsWithoutExecutor()
    {
        var clock = new SystemClock();
        var repository = new InMemoryAgentRunRepository();
        var registry = new ToolRegistry();
        var policy = new RuntimePolicyEvaluator(registry, clock, Microsoft.Extensions.Options.Options.Create(new RuntimePolicyOptions()));
        var handler = new StartAgentRunHandler(repository, policy, registry, clock);

        var run = await handler.HandleAsync(
            new StartAgentRunCommand("PR1 Agent", "No registered tool.", "no-tool-trace"),
            CancellationToken.None);

        Assert.Equal(AgentRunStatus.Failed, run.Status);
        Assert.Empty(run.Steps[0].ToolCalls);
    }
}