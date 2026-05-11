using Agentor.Application;
using Agentor.Application.Abstractions;
using Agentor.Application.Commands;
using Agentor.Application.Options;
using Agentor.Application.RunQueue;
using Agentor.Domain;
using Agentor.Domain.Enums;
using Agentor.Infrastructure;
using Agentor.Infrastructure.Management;
using Agentor.Infrastructure.Persistence;
using Agentor.Infrastructure.RunQueue;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Text.Json.Nodes;

namespace Agentor.Infrastructure.Tests;

/// <summary>Phase 25 — EF-backed scoped queue/lease with <see cref="ServiceProviderOptions.ValidateScopes"/>.</summary>
public sealed class RunQueueHostedServiceEfSqliteScopeTests
{
    [Fact]
    public async Task TryProcessSingleAsync_WithEfStoresAndValidateScopes_ClaimsExecutesAndCompletes()
    {
        await DrainAndAssertCompletedAsync(new StartAgentRunCommand("Scoped EF worker", "Drain one item."));
    }

    [Fact]
    public async Task TryProcessSingleAsync_EnqueuedConexusModelTool_Completes()
    {
        await DrainAndAssertCompletedAsync(new StartAgentRunCommand(
            "Model worker",
            "Queue-stored model tool.",
            "efq-model",
            ToolKey: WellKnownToolKeys.ConexusModelComplete));
    }

    [Fact]
    public async Task TryProcessSingleAsync_EnqueuedMcpEcho_Completes()
    {
        var echoKey = McpToolKeys.Format("demo-server", "echo");
        await DrainAndAssertCompletedAsync(new StartAgentRunCommand(
            "MCP worker",
            "Queue-stored MCP echo.",
            "efq-mcp",
            ToolKey: echoKey,
            ToolInput: new Dictionary<string, string> { ["text"] = "queued-ping" }));
    }

    [Fact]
    public async Task TryProcessSingleAsync_EnqueuedMcpEcho_StructuredToolPayload_Completes()
    {
        var echoKey = McpToolKeys.Format("demo-server", "echo");
        var body = JsonNode.Parse("""{"text":"queued-structured"}""")!.AsObject();
        var payload = new ToolPayload(body, "urn:agentor:worker:test", "application/json", null);
        await DrainAndAssertCompletedAsync(new StartAgentRunCommand(
            "MCP structured worker",
            "Queue-stored MCP echo with ToolPayload.",
            "efq-mcp-struct",
            ToolKey: echoKey,
            ToolInputPayload: payload));
    }

    [Fact]
    public async Task TryProcessSingleAsync_EnqueuedLegacyExplicit_Completes()
    {
        await DrainAndAssertCompletedAsync(
            new StartAgentRunCommand(
                "Legacy worker",
                "Explicit legacy from queue.",
                "efq-legacy",
                Mode: RunExecutionMode.LegacyFakeTool),
            configOverrides: new Dictionary<string, string?>
            {
                ["Agentor:PublicRuns:TreatMissingExecutionSelectorAsLegacyFakeTool"] = "false",
            });
    }

    [Fact]
    public async Task TryProcessSingleAsync_EnqueuedRecipe_Completes()
    {
        var recipeId = Guid.NewGuid();
        await DrainAndAssertCompletedAsync(
            new StartAgentRunCommand(
                "Recipe worker",
                "Queued recipe execution.",
                "efq-recipe",
                RecipeId: recipeId),
            configureRoot: sp =>
            {
                var store = sp.GetRequiredService<InMemoryManagementRecipeStore>();
                var ok = AgentRecipe.TryCreate(
                    recipeId,
                    "queued-recipe",
                    AgentRecipeVersion.Parse("1.0"),
                    CoordinationTopology.SequentialPipeline,
                    [new RecipeStepDefinition("s1", 1, RecipeStepKind.Tool, WellKnownToolKeys.Pr1FakeTool)],
                    profileRef: null,
                    out var recipe,
                    out _);
                Assert.True(ok);
                Assert.True(store.TryAdd(recipe!));
            });
    }

    private static async Task DrainAndAssertCompletedAsync(
        StartAgentRunCommand command,
        IReadOnlyDictionary<string, string?>? configOverrides = null,
        Action<IServiceProvider>? configureRoot = null)
    {
        var dbFile = Path.GetTempFileName();
        try
        {
            var cs = $"Data Source={dbFile}";

            var settings = new Dictionary<string, string?>
            {
                ["Agentor:RunQueue:ExecutionMode"] = "DurableBackground",
                ["Agentor:RunWorker:Enabled"] = "false",
                ["Agentor:PublicRuns:TreatMissingExecutionSelectorAsLegacyFakeTool"] = "true",
            };
            if (configOverrides is not null)
            {
                foreach (var kv in configOverrides)
                {
                    settings[kv.Key] = kv.Value;
                }
            }

            var config = new ConfigurationBuilder().AddInMemoryCollection(settings!).Build();

            var services = new ServiceCollection();
            services.AddAgentorApplication();
            services.AddAgentorInfrastructure(config);
            services.AddAgentorEfCoreRepository(options => options.UseSqlite(cs));

            await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

            configureRoot?.Invoke(provider);

            await using (var initScope = provider.CreateAsyncScope())
            {
                var db = initScope.ServiceProvider.GetRequiredService<AgentorDbContext>();
                await db.Database.EnsureCreatedAsync();
            }

            var clock = provider.GetRequiredService<IClock>();
            var workItem = new RunWorkItem(Guid.NewGuid(), command);

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
