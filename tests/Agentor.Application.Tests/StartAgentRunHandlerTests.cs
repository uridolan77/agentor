using Agentor.Application.Commands;
using Agentor.Domain.Enums;
using Agentor.Infrastructure;

namespace Agentor.Application.Tests;

public sealed class StartAgentRunHandlerTests
{
    [Fact]
    public async Task HandleAsync_CreatesCompletedRun_WithPolicyToolAndTrace()
    {
        var clock = new SystemClock();
        var repository = new InMemoryAgentRunRepository();
        var policy = new AllowAllPolicyEvaluator(clock);
        var tool = new FakeToolExecutor();
        var handler = new StartAgentRunHandler(repository, policy, tool, clock);

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
}
