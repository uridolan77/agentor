using Agentor.Application.Abstractions;
using Agentor.Application.Options;
using Agentor.Application.Reliability;
using Agentor.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

namespace Agentor.Application.Tests;

public sealed class OutboxDispatcherTests
{
    [Fact]
    public async Task DispatchPendingAsync_OnSuccess_MarksSucceeded()
    {
        var store = new InMemoryOutboxStore();
        var id = Guid.NewGuid();
        var msg = new OutboxMessage(id, OutboxMessageKind.Mcp, "{}", OutboxStatus.Pending, 0, DateTimeOffset.UtcNow, null);
        await store.AppendAsync(msg, CancellationToken.None);

        var dispatcher = new OutboxDispatcher(
            store,
            Microsoft.Extensions.Options.Options.Create(new OutboxDispatcherOptions { MaxDispatchAttempts = 5 }));

        var sink = new RecordingSink();
        var n = await dispatcher.DispatchPendingAsync(sink, 10, CancellationToken.None);

        Assert.Equal(1, n);
        Assert.Single(sink.Ids);
        Assert.Equal(id, sink.Ids[0]);

        var pending = await store.ListPendingForDispatchAsync(10, CancellationToken.None);
        Assert.Empty(pending);
    }

    [Fact]
    public async Task DispatchPendingAsync_OnRepeatedFailures_MarksPoisonAtMaxAttempts()
    {
        var store = new InMemoryOutboxStore();
        var id = Guid.NewGuid();
        var msg = new OutboxMessage(id, OutboxMessageKind.Athanor, "{}", OutboxStatus.Pending, 0, DateTimeOffset.UtcNow, null);
        await store.AppendAsync(msg, CancellationToken.None);

        var dispatcher = new OutboxDispatcher(
            store,
            Microsoft.Extensions.Options.Options.Create(new OutboxDispatcherOptions { MaxDispatchAttempts = 2 }));

        var sink = new ThrowingSink("boom");
        _ = await dispatcher.DispatchPendingAsync(sink, 10, CancellationToken.None);
        _ = await dispatcher.DispatchPendingAsync(sink, 10, CancellationToken.None);

        var pending = await store.ListPendingForDispatchAsync(10, CancellationToken.None);
        Assert.Empty(pending);

        var dispatching = await store.TryMarkDispatchingAsync(id, CancellationToken.None);
        Assert.False(dispatching);
    }

    private sealed class RecordingSink : IOutboxSink
    {
        public List<Guid> Ids { get; } = new();

        public Task SendAsync(OutboxMessage message, CancellationToken cancellationToken)
        {
            Ids.Add(message.Id);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingSink(string message) : IOutboxSink
    {
        public Task SendAsync(OutboxMessage _, CancellationToken cancellationToken) =>
            Task.FromException(new InvalidOperationException(message));
    }
}
