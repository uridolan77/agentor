using Agentor.Domain;
using Agentor.Domain.Enums;

namespace Agentor.Domain.Tests;

public sealed class AgentRunSessionMemoryTests
{
    [Fact]
    public void TryWriteSessionMemory_AcceptsWithinBudget()
    {
        var run = AgentRun.Start(Guid.NewGuid(), "a", "o", "t", DateTimeOffset.UtcNow);
        var r = run.TryWriteSessionMemory("note", "hello", SessionMemoryBudget.Default, DateTimeOffset.UtcNow);
        Assert.Equal(SessionMemoryWriteStatus.Accepted, r.Status);
        Assert.Equal("hello", run.SessionMemory["note"]);
        Assert.Contains(run.Trace, e => e.Kind == TraceEventKind.SessionMemoryWriteAccepted);
    }

    [Fact]
    public void TryWriteSessionMemory_RejectsWhenOverValueLength()
    {
        var run = AgentRun.Start(Guid.NewGuid(), "a", "o", "t", DateTimeOffset.UtcNow);
        var budget = new SessionMemoryBudget(10, 10, 4, 1000);
        var r = run.TryWriteSessionMemory("k", "12345", budget, DateTimeOffset.UtcNow);
        Assert.Equal(SessionMemoryWriteStatus.RejectedValueTooLarge, r.Status);
    }
}