using Agentor.Application.Commands;
using Agentor.Application.RunQueue;
using Agentor.Domain.Enums;
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
    public async Task EnqueueAsync_RoundTripsOrchestrationSelectorsAndToolInput()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await using var ctx = await CreateSqliteContextAsync(connection);
        var store = new EfRunQueueStore(ctx);

        var recipeId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var toolInput = new Dictionary<string, string> { ["text"] = "hi" };

        var workItem = new RunWorkItem(
            Guid.NewGuid(),
            new StartAgentRunCommand(
                "Payload Agent",
                "Round-trip selectors.",
                "queue-payload-trace",
                TenantId: Guid.NewGuid(),
                WorkspaceId: Guid.NewGuid(),
                ProjectId: Guid.NewGuid(),
                KnowledgeScopeId: Guid.NewGuid(),
                Mode: RunExecutionMode.ModelCall,
                RecipeId: recipeId,
                PlanId: planId,
                ToolKey: "conexus.model-complete",
                SkillKey: "unused-skill",
                ToolInput: toolInput));

        await store.EnqueueAsync(workItem, DateTimeOffset.UtcNow, CancellationToken.None);

        var loaded = await store.GetAsync(workItem.WorkItemId, CancellationToken.None);
        Assert.NotNull(loaded);
        var cmd = loaded!.Command;
        Assert.Equal(RunExecutionMode.ModelCall, cmd.Mode);
        Assert.Equal(recipeId, cmd.RecipeId);
        Assert.Equal(planId, cmd.PlanId);
        Assert.Equal("conexus.model-complete", cmd.ToolKey);
        Assert.Equal("unused-skill", cmd.SkillKey);
        Assert.NotNull(cmd.ToolInput);
        Assert.Equal("hi", cmd.ToolInput!["text"]);
    }

}
