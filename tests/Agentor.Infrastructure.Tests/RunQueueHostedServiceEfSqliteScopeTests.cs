using Agentor.Application;
using Agentor.Application.Abstractions;
using Agentor.Application.Commands;
using Agentor.Application.Options;
using Agentor.Application.RunQueue;
using Agentor.Infrastructure;
using Agentor.Infrastructure.Persistence;
using Agentor.Infrastructure.RunQueue;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Agentor.Infrastructure.Tests;

/// <summary>Phase 25 — EF-backed scoped queue/lease with <see cref="ServiceProviderOptions.ValidateScopes"/>.</summary>
public sealed class RunQueueHostedServiceEfSqliteScopeTests
{
    [Fact]
    public async Task TryProcessSingleAsync_WithEfStoresAndValidateScopes_ClaimsExecutesAndCompletes()
    {
        var dbFile = Path.GetTempFileName();
        try
        {
            var cs = $"Data Source={dbFile}";

            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Agentor:RunQueue:ExecutionMode"] = "DurableBackground",
                ["Agentor:RunWorker:Enabled"] = "false",
                ["Agentor:PublicRuns:TreatMissingExecutionSelectorAsLegacyFakeTool"] = "true",
            }).Build();

            var services = new ServiceCollection();
            services.AddAgentorApplication();
            services.AddAgentorInfrastructure(config);
            services.AddAgentorEfCoreRepository(options => options.UseSqlite(cs));

            await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

            await using (var initScope = provider.CreateAsyncScope())
            {
                var db = initScope.ServiceProvider.GetRequiredService<AgentorDbContext>();
                await db.Database.EnsureCreatedAsync();
            }

            var clock = provider.GetRequiredService<IClock>();
            var workItem = new RunWorkItem(Guid.NewGuid(), new StartAgentRunCommand("Scoped EF worker", "Drain one item."));

            await using (var enqueueScope = provider.CreateAsyncScope())
            {
                var queue = enqueueScope.ServiceProvider.GetRequiredService<IDurableRunQueue>();
                await queue.EnqueueAsync(workItem, clock.UtcNow, CancellationToken.None);
            }

            var svc = new RunQueueHostedService(
                provider.GetRequiredService<IServiceScopeFactory>(),
                clock,
                new StaticMonitor<RunQueueOptions>(new RunQueueOptions { ExecutionMode = RunQueueExecutionMode.DurableBackground }),
                new StaticMonitor<RunWorkerOptions>(new RunWorkerOptions { Enabled = true, LeaseTtlSeconds = 30 }));

            var processed = await svc.TryProcessSingleAsync(CancellationToken.None);
            Assert.True(processed);

            await using (var verifyScope = provider.CreateAsyncScope())
            {
                var queue = verifyScope.ServiceProvider.GetRequiredService<IDurableRunQueue>();
                var snapshot = await queue.GetAsync(workItem.WorkItemId, CancellationToken.None);
                Assert.NotNull(snapshot);
                Assert.Equal(DurableRunQueueStatus.Completed, snapshot!.Status);
                Assert.NotNull(snapshot.AgentRunId);
            }
        }
        finally
        {
            TryDelete(dbFile);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup for file-backed sqlite tests.
        }
    }

    private sealed class StaticMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue { get; } = value;

        public T Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
