using Agentor.Application;
using Agentor.Application.Abstractions;
using Agentor.Application.Options;
using Agentor.Application.Reliability;
using Agentor.Infrastructure.Persistence;
using Agentor.Infrastructure.Reliability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Agentor.Infrastructure.Tests;

public sealed class OutboxHostedServiceTests
{
    [Fact]
    public async Task DispatchOnceAsync_DisabledByDefault_DoesNothing()
    {
        var store = new InMemoryOutboxStore();
        var provider = BuildProvider(store, new RecordingSink());

        var msg = new OutboxMessage(Guid.NewGuid(), OutboxMessageKind.Mcp, "{}", OutboxStatus.Pending, 0, DateTimeOffset.UtcNow, null);
        await store.AppendAsync(msg, CancellationToken.None);

        var svc = new OutboxHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new StaticMonitor<OutboxDispatchOptions>(new OutboxDispatchOptions { Enabled = false }));

        var dispatched = await svc.DispatchOnceAsync(CancellationToken.None);
        Assert.False(dispatched);

        var pending = await store.ListPendingForDispatchAsync(10, CancellationToken.None);
        Assert.Single(pending);
    }

    [Fact]
    public async Task DispatchOnceAsync_Enabled_DispatchesPendingMessage()
    {
        var store = new InMemoryOutboxStore();
        var sink = new RecordingSink();
        var provider = BuildProvider(store, sink);

        var msg = new OutboxMessage(Guid.NewGuid(), OutboxMessageKind.Athanor, "{}", OutboxStatus.Pending, 0, DateTimeOffset.UtcNow, null);
        await store.AppendAsync(msg, CancellationToken.None);

        var svc = new OutboxHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new StaticMonitor<OutboxDispatchOptions>(new OutboxDispatchOptions { Enabled = true, BatchSize = 10 }));

        var dispatched = await svc.DispatchOnceAsync(CancellationToken.None);
        Assert.True(dispatched);
        Assert.Single(sink.Ids);

        var pending = await store.ListPendingForDispatchAsync(10, CancellationToken.None);
        Assert.Empty(pending);
    }

    [Fact]
    public async Task DispatchOnceAsync_Enabled_RetriesAndPoisonsOnFailures()
    {
        var store = new InMemoryOutboxStore();
        var provider = BuildProvider(store, new ThrowingSink("boom"));

        var id = Guid.NewGuid();
        var msg = new OutboxMessage(id, OutboxMessageKind.Conexus, "{}", OutboxStatus.Pending, 0, DateTimeOffset.UtcNow, null);
        await store.AppendAsync(msg, CancellationToken.None);

        var svc = new OutboxHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new StaticMonitor<OutboxDispatchOptions>(new OutboxDispatchOptions { Enabled = true, BatchSize = 10 }));

        _ = await svc.DispatchOnceAsync(CancellationToken.None);
        _ = await svc.DispatchOnceAsync(CancellationToken.None);

        var latest = await store.ListLatestAsync(10, CancellationToken.None);
        var row = latest.Single(x => x.Id == id);
        Assert.Equal(OutboxStatus.Poison, row.Status);
        Assert.Equal(2, row.AttemptCount);
    }

    private static ServiceProvider BuildProvider(IOutboxStore store, IOutboxSink sink)
    {
        var inMemoryStore = (InMemoryOutboxStore)store;
        var services = new ServiceCollection();
        services.AddAgentorApplication();
        services.AddSingleton(inMemoryStore);
        services.AddSingleton<IOutboxStore>(sp => sp.GetRequiredService<InMemoryOutboxStore>());
        services.AddSingleton<IOutboxSink>(sink);
        services.Configure<OutboxDispatcherOptions>(o => o.MaxDispatchAttempts = 2);
        return services.BuildServiceProvider();
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

    private sealed class ThrowingSink(string errorMessage) : IOutboxSink
    {
        public Task SendAsync(OutboxMessage message, CancellationToken cancellationToken) =>
            Task.FromException(new InvalidOperationException(errorMessage));
    }

    private sealed class StaticMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue { get; } = value;
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
