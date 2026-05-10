using Agentor.Domain;
using Agentor.Domain.Enums;

namespace Agentor.Domain.Tests;

public sealed class AgentRunTests
{
    [Fact]
    public void Start_CreatesRunningRun_WithInitialTrace()
    {
        var run = AgentRun.Start(Guid.NewGuid(), "Test Agent", "Test objective", "trace-1", DateTimeOffset.UtcNow);

        Assert.Equal(AgentRunStatus.Running, run.Status);
        Assert.NotEqual(Guid.Empty, run.Id);
        Assert.Single(run.Trace);
        Assert.Equal(TraceEventKind.RunStarted, run.Trace[0].Kind);
    }

    [Fact]
    public void Complete_RequiresAtLeastOneStep()
    {
        var run = AgentRun.Start(Guid.NewGuid(), "Test Agent", "Test objective", "trace-1", DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => run.Complete(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void StartStep_AddsRunningStep()
    {
        var run = AgentRun.Start(Guid.NewGuid(), "Test Agent", "Test objective", "trace-1", DateTimeOffset.UtcNow);

        var step = run.StartStep("Step one", DateTimeOffset.UtcNow);

        Assert.Single(run.Steps);
        Assert.Equal(AgentStepStatus.Running, step.Status);
        Assert.Equal(1, step.Index);
    }
}
