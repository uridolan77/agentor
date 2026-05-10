using Agentor.Application.Abstractions;
using Agentor.Domain;
using Agentor.Domain.Enums;
using Agentor.Infrastructure;
using Agentor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Agentor.Infrastructure.Tests;

public abstract class AgentRunRepositoryContractTests
{
    protected abstract Task<(IAgentRunRepository Repo, IAsyncDisposable? Cleanup)> CreateRepositoryAsync();

    [Fact]
    public async Task SaveAsync_GetAsync_RoundTripsCompletedRun()
    {
        var (repo, cleanup) = await CreateRepositoryAsync();
        try
        {
            var profile = AgentProfile.Create("Contract Agent", "Contract test.", DateTimeOffset.UtcNow);
            var run = AgentRun.Start(profile.Id, profile.Name, "Contract objective.", "contract-trace", DateTimeOffset.UtcNow);
            var step = run.StartStep("Step", DateTimeOffset.UtcNow);
            step.Complete(DateTimeOffset.UtcNow);
            run.Complete(DateTimeOffset.UtcNow);

            await repo.SaveAsync(run, CancellationToken.None);
            var loaded = await repo.GetAsync(run.Id, CancellationToken.None);

            Assert.NotNull(loaded);
            Assert.Equal(run.Id, loaded!.Id);
            Assert.Equal(AgentRunStatus.Completed, loaded.Status);
            Assert.Equal("contract-trace", loaded.TraceId);
        }
        finally
        {
            if (cleanup is not null)
            {
                await cleanup.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task ListSummariesAsync_ReturnsNewestFirst_WithPaging()
    {
        var (repo, cleanup) = await CreateRepositoryAsync();
        try
        {
            var profile = AgentProfile.Create("P", "P", DateTimeOffset.UtcNow);
            var run1 = AgentRun.Start(profile.Id, profile.Name, "One.", "t1", DateTimeOffset.UtcNow);
            run1.StartStep("s", DateTimeOffset.UtcNow).Complete(DateTimeOffset.UtcNow);
            run1.Complete(DateTimeOffset.UtcNow);
            await Task.Delay(5);
            var run2 = AgentRun.Start(profile.Id, profile.Name, "Two.", "t2", DateTimeOffset.UtcNow);
            run2.StartStep("s", DateTimeOffset.UtcNow).Complete(DateTimeOffset.UtcNow);
            run2.Complete(DateTimeOffset.UtcNow);

            await repo.SaveAsync(run1, CancellationToken.None);
            await repo.SaveAsync(run2, CancellationToken.None);

            var page = await repo.ListSummariesAsync(0, 10, CancellationToken.None);
            Assert.Equal(2, page.TotalCount);
            Assert.True(page.Items[0].StartedAt >= page.Items[1].StartedAt);
        }
        finally
        {
            if (cleanup is not null)
            {
                await cleanup.DisposeAsync();
            }
        }
    }
}

public sealed class InMemoryAgentRunRepositoryContractTests : AgentRunRepositoryContractTests
{
    protected override Task<(IAgentRunRepository Repo, IAsyncDisposable? Cleanup)> CreateRepositoryAsync()
    {
        IAgentRunRepository repo = new InMemoryAgentRunRepository();
        return Task.FromResult((repo, (IAsyncDisposable?)null));
    }
}

public sealed class EfCoreInMemoryAgentRunRepositoryContractTests : AgentRunRepositoryContractTests
{
    protected override async Task<(IAgentRunRepository Repo, IAsyncDisposable? Cleanup)> CreateRepositoryAsync()
    {
        var opts = new DbContextOptionsBuilder<AgentorDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var ctx = new AgentorDbContext(opts);
        await ctx.Database.EnsureCreatedAsync();
        IAgentRunRepository repo = new EfCoreAgentRunRepository(ctx);
        return (repo, ctx);
    }
}