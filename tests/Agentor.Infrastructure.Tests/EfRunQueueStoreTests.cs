using Agentor.Application.RunQueue;
using Agentor.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Agentor.Infrastructure.Tests;

public sealed class EfRunQueueStoreTests
{
    private static async Task<AgentorDbContext> CreateSqliteContextAsync(SqliteConnection connection)
    {
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AgentorDbContext>()
            .UseSqlite(connection)
            .Options;

        var ctx = new AgentorDbContext(options);
        await ctx.Database.EnsureCreatedAsync();
        return ctx;
    }

    [Fact]
    public async Task EnqueueAsync_PersistsQueueItem()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await using var ctx = await CreateSqliteContextAsync(connection);
        var store = new EfRunQueueStore(ctx);

        var workItem = new RunWorkItem(
            Guid.NewGuid(),
            new Agentor.Application.Commands.StartAgentRunCommand("Queue Agent", "Persist queue item."));

        await store.EnqueueAsync(workItem, DateTimeOffset.UtcNow, CancellationToken.None);

        var loaded = await store.GetAsync(workItem.WorkItemId, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal(DurableRunQueueStatus.Pending, loaded!.Status);
    }

    [Fact]
    public async Task TryClaimNextAsync_ClaimsPendingItem()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await using var ctx = await CreateSqliteContextAsync(connection);
        var store = new EfRunQueueStore(ctx);

        var workItem = new RunWorkItem(
            Guid.NewGuid(),
            new Agentor.Application.Commands.StartAgentRunCommand("Queue Agent", "Claim queue item."));

        var now = DateTimeOffset.UtcNow;
        await store.EnqueueAsync(workItem, now, CancellationToken.None);

        var claimed = await store.TryClaimNextAsync("worker-a", TimeSpan.FromSeconds(30), now, CancellationToken.None);
        Assert.NotNull(claimed);
        Assert.Equal(DurableRunQueueStatus.Claimed, claimed!.Status);
        Assert.Equal("worker-a", claimed.ClaimedBy);
    }

    [Fact]
    public async Task QueueSurvivesRestart_WithSeparateDbContexts()
    {
        var dbFile = Path.GetTempFileName();
        var cs = $"Data Source={dbFile}";

        try
        {
            await using (var seed = new AgentorDbContext(new DbContextOptionsBuilder<AgentorDbContext>().UseSqlite(cs).Options))
            {
                await seed.Database.EnsureCreatedAsync();
                var store = new EfRunQueueStore(seed);
                var workItem = new RunWorkItem(
                    Guid.NewGuid(),
                    new Agentor.Application.Commands.StartAgentRunCommand("Restart Agent", "Durable queue survives restart."));
                await store.EnqueueAsync(workItem, DateTimeOffset.UtcNow, CancellationToken.None);
            }

            await using var afterRestart = new AgentorDbContext(new DbContextOptionsBuilder<AgentorDbContext>().UseSqlite(cs).Options);
            var restartStore = new EfRunQueueStore(afterRestart);
            var claimed = await restartStore.TryClaimNextAsync(
                "worker-restart",
                TimeSpan.FromSeconds(30),
                DateTimeOffset.UtcNow,
                CancellationToken.None);

            Assert.NotNull(claimed);
            Assert.Equal("worker-restart", claimed!.ClaimedBy);
        }
        finally
        {
            if (File.Exists(dbFile))
            {
                try
                {
                    File.Delete(dbFile);
                }
                catch (IOException)
                {
                    // Best-effort cleanup for file-backed sqlite tests.
                }
            }
        }
    }

    [Fact]
    public async Task TryClaimNextAsync_ReclaimsExpiredClaimedItem()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await using var ctx = await CreateSqliteContextAsync(connection);
        var store = new EfRunQueueStore(ctx);

        var workItemId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await store.EnqueueAsync(
            new RunWorkItem(workItemId, new Agentor.Application.Commands.StartAgentRunCommand("Queue Agent", "Expired claim reclaim.")),
            now.AddMinutes(-10),
            CancellationToken.None);

        _ = await store.TryClaimAsync(workItemId, "worker-a", TimeSpan.FromSeconds(1), now.AddMinutes(-5), CancellationToken.None);

        var reclaimed = await store.TryClaimNextAsync("worker-b", TimeSpan.FromSeconds(30), now, CancellationToken.None);
        Assert.NotNull(reclaimed);
        Assert.Equal(workItemId, reclaimed!.WorkItemId);
        Assert.Equal("worker-b", reclaimed.ClaimedBy);
    }

    [Fact]
    public async Task TryClaimNextAsync_DoesNotStealNonExpiredClaimedItem()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await using var ctx = await CreateSqliteContextAsync(connection);
        var store = new EfRunQueueStore(ctx);

        var workItemId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await store.EnqueueAsync(
            new RunWorkItem(workItemId, new Agentor.Application.Commands.StartAgentRunCommand("Queue Agent", "Non-expired claim protection.")),
            now,
            CancellationToken.None);

        _ = await store.TryClaimAsync(workItemId, "worker-a", TimeSpan.FromMinutes(5), now, CancellationToken.None);

        var stolen = await store.TryClaimNextAsync("worker-b", TimeSpan.FromSeconds(30), now, CancellationToken.None);
        Assert.Null(stolen);
    }

    [Fact]
    public async Task MarkCompletedAsync_RequiresClaimOwner()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await using var ctx = await CreateSqliteContextAsync(connection);
        var store = new EfRunQueueStore(ctx);

        var workItemId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await store.EnqueueAsync(
            new RunWorkItem(workItemId, new Agentor.Application.Commands.StartAgentRunCommand("Queue Agent", "Owner check completion.")),
            now,
            CancellationToken.None);
        _ = await store.TryClaimAsync(workItemId, "worker-a", TimeSpan.FromMinutes(1), now, CancellationToken.None);

        await store.MarkCompletedAsync(workItemId, Guid.NewGuid(), "worker-b", DateTimeOffset.UtcNow, CancellationToken.None);
        var rowAfterWrongOwner = await store.GetAsync(workItemId, CancellationToken.None);
        Assert.NotNull(rowAfterWrongOwner);
        Assert.Equal(DurableRunQueueStatus.Claimed, rowAfterWrongOwner!.Status);

        await store.MarkCompletedAsync(workItemId, Guid.NewGuid(), "worker-a", DateTimeOffset.UtcNow, CancellationToken.None);
        var rowAfterOwner = await store.GetAsync(workItemId, CancellationToken.None);
        Assert.NotNull(rowAfterOwner);
        Assert.Equal(DurableRunQueueStatus.Completed, rowAfterOwner!.Status);
    }

    [Fact]
    public async Task MarkFailedAsync_RequiresClaimOwner()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await using var ctx = await CreateSqliteContextAsync(connection);
        var store = new EfRunQueueStore(ctx);

        var workItemId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await store.EnqueueAsync(
            new RunWorkItem(workItemId, new Agentor.Application.Commands.StartAgentRunCommand("Queue Agent", "Owner check failure.")),
            now,
            CancellationToken.None);
        _ = await store.TryClaimAsync(workItemId, "worker-a", TimeSpan.FromMinutes(1), now, CancellationToken.None);

        await store.MarkFailedAsync(workItemId, "boom", "worker-b", DateTimeOffset.UtcNow, CancellationToken.None);
        var rowAfterWrongOwner = await store.GetAsync(workItemId, CancellationToken.None);
        Assert.NotNull(rowAfterWrongOwner);
        Assert.Equal(DurableRunQueueStatus.Claimed, rowAfterWrongOwner!.Status);

        await store.MarkFailedAsync(workItemId, "boom", "worker-a", DateTimeOffset.UtcNow, CancellationToken.None);
        var rowAfterOwner = await store.GetAsync(workItemId, CancellationToken.None);
        Assert.NotNull(rowAfterOwner);
        Assert.Equal(DurableRunQueueStatus.Failed, rowAfterOwner!.Status);
    }
}
