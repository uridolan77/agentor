using Agentor.Application.Commands;
using Agentor.Application.RunQueue;
using Agentor.Domain;
using Agentor.Domain.Enums;
using Agentor.Infrastructure.Persistence;
using Agentor.Infrastructure.Persistence.Records;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Nodes;

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

    [Fact]
    public async Task EnqueueAsync_RoundTripsStructuredToolPayload_InToolPayloadJsonColumn()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await using var ctx = await CreateSqliteContextAsync(connection);
        var store = new EfRunQueueStore(ctx);

        var body = JsonNode.Parse("""{"text":"structured-queue"}""")!.AsObject();
        var payload = new ToolPayload(
            body,
            schemaId: "urn:agentor:test:queue-payload",
            contentType: "application/json",
            summary: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["tier"] = "gold" });

        var workItem = new RunWorkItem(
            Guid.NewGuid(),
            new StartAgentRunCommand(
                "Structured Agent",
                "Persist ToolPayload v2.",
                "queue-v2-trace",
                ToolKey: "mcp.demo-server.echo",
                ToolInputPayload: payload));

        await store.EnqueueAsync(workItem, DateTimeOffset.UtcNow, CancellationToken.None);

        var row = await ctx.RunQueueItems.AsNoTracking()
            .SingleAsync(r => r.WorkItemId == workItem.WorkItemId);

        Assert.NotNull(row.ToolPayloadJson);
        Assert.Contains("urn:agentor:test:queue-payload", row.ToolPayloadJson, StringComparison.Ordinal);
        Assert.Contains("structured-queue", row.ToolPayloadJson, StringComparison.Ordinal);

        var loaded = await store.GetAsync(workItem.WorkItemId, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Null(loaded!.Command.ToolInput);
        Assert.NotNull(loaded.Command.ToolInputPayload);
        Assert.Equal("structured-queue", loaded.Command.ToolInputPayload.Body["text"]!.GetValue<string>());
        Assert.Equal("gold", loaded.Command.ToolInputPayload.Summary["tier"]);
    }

    [Fact]
    public async Task GetAsync_LegacyRowsWithoutToolPayloadJson_UseToolInputOnly()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await using var ctx = await CreateSqliteContextAsync(connection);

        var id = Guid.NewGuid();
        ctx.RunQueueItems.Add(new RunQueueItemRecord
        {
            WorkItemId = id,
            AgentName = "Legacy-only queue row",
            Objective = "Obj",
            Status = DurableRunQueueStatus.Pending.ToString(),
            EnqueuedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            ToolKey = "tool.key",
            ToolInputJson = """{"text":"legacy-row"}""",
            ToolPayloadJson = null,
        });
        await ctx.SaveChangesAsync();

        var store = new EfRunQueueStore(ctx);
        var loaded = await store.GetAsync(id, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Null(loaded!.Command.ToolInputPayload);
        Assert.NotNull(loaded.Command.ToolInput);
        Assert.Equal("legacy-row", loaded.Command.ToolInput["text"]);
    }
}
