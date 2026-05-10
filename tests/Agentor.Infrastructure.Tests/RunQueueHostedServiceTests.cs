using Agentor.Application;
using Agentor.Application.Abstractions;
using Agentor.Application.Commands;
using Agentor.Application.Options;
using Agentor.Application.RunQueue;
using Agentor.Infrastructure;
using Agentor.Infrastructure.Persistence;
using Agentor.Infrastructure.RunQueue;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Agentor.Infrastructure.Tests;

public sealed class RunQueueHostedServiceTests
{
    [Fact]
    public async Task TryProcessSingleAsync_DisabledByDefault_DoesNothing()
    {
        var serviceProvider = BuildProvider();
        var queue = serviceProvider.GetRequiredService<IDurableRunQueue>();
        var workItem = new RunWorkItem(Guid.NewGuid(), new StartAgentRunCommand("Agent", "Disabled worker test."));
        await queue.EnqueueAsync(workItem, DateTimeOffset.UtcNow, CancellationToken.None);

        var svc = new RunQueueHostedService(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            serviceProvider.GetRequiredService<IClock>(),
            new StaticMonitor<RunQueueOptions>(new RunQueueOptions { ExecutionMode = RunQueueExecutionMode.DurableBackground }),
            new StaticMonitor<RunWorkerOptions>(new RunWorkerOptions { Enabled = false }));

        var processed = await svc.TryProcessSingleAsync(CancellationToken.None);
        Assert.False(processed);

        var snapshot = await queue.GetAsync(workItem.WorkItemId, CancellationToken.None);
        Assert.NotNull(snapshot);
        Assert.Equal(DurableRunQueueStatus.Pending, snapshot!.Status);
    }

    [Fact]
    public async Task TryProcessSingleAsync_Enabled_ProcessesQueuedWork()
    {
        var serviceProvider = BuildProvider();
        var queue = serviceProvider.GetRequiredService<IDurableRunQueue>();
        var workItem = new RunWorkItem(Guid.NewGuid(), new StartAgentRunCommand("Agent", "Enabled worker test."));
        await queue.EnqueueAsync(workItem, DateTimeOffset.UtcNow, CancellationToken.None);

        var svc = new RunQueueHostedService(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            serviceProvider.GetRequiredService<IClock>(),
            new StaticMonitor<RunQueueOptions>(new RunQueueOptions { ExecutionMode = RunQueueExecutionMode.DurableBackground }),
            new StaticMonitor<RunWorkerOptions>(new RunWorkerOptions { Enabled = true, LeaseTtlSeconds = 30 }));

        var processed = await svc.TryProcessSingleAsync(CancellationToken.None);
        Assert.True(processed);

        var snapshot = await queue.GetAsync(workItem.WorkItemId, CancellationToken.None);
        Assert.NotNull(snapshot);
        Assert.Equal(DurableRunQueueStatus.Completed, snapshot!.Status);
        Assert.NotNull(snapshot.AgentRunId);
    }

    [Fact]
    public async Task TryProcessSingleAsync_ContestedLease_ReleasesClaimWithoutProcessing()
    {
        var serviceProvider = BuildProvider();
        var queue = serviceProvider.GetRequiredService<IDurableRunQueue>();
        var leases = serviceProvider.GetRequiredService<IRunExecutionLeaseStore>();
        var clock = serviceProvider.GetRequiredService<IClock>();

        var workItem = new RunWorkItem(Guid.NewGuid(), new StartAgentRunCommand("Agent", "Lease contention test."));
        var now = clock.UtcNow;
        await queue.EnqueueAsync(workItem, now, CancellationToken.None);

        var acquired = await leases.TryAcquireAsync(workItem.WorkItemId, "other-worker", TimeSpan.FromMinutes(5), now, CancellationToken.None);
        Assert.Equal(LeaseAcquireOutcome.Acquired, acquired);

        var svc = new RunQueueHostedService(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            clock,
            new StaticMonitor<RunQueueOptions>(new RunQueueOptions { ExecutionMode = RunQueueExecutionMode.DurableBackground }),
            new StaticMonitor<RunWorkerOptions>(new RunWorkerOptions { Enabled = true, LeaseTtlSeconds = 30 }));

        var processed = await svc.TryProcessSingleAsync(CancellationToken.None);
        Assert.False(processed);

        var snapshot = await queue.GetAsync(workItem.WorkItemId, CancellationToken.None);
        Assert.NotNull(snapshot);
        Assert.Equal(DurableRunQueueStatus.Pending, snapshot!.Status);
        Assert.Null(snapshot.AgentRunId);
    }

    private static ServiceProvider BuildProvider()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Agentor:RunQueue:ExecutionMode"] = "DurableBackground",
            ["Agentor:RunWorker:Enabled"] = "false",
        }).Build();

        var services = new ServiceCollection();
        services.AddAgentorApplication();
        services.AddAgentorInfrastructure(config);
        return services.BuildServiceProvider();
    }

    private sealed class StaticMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue { get; } = value;
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
