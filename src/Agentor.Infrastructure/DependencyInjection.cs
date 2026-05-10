using System.Linq;
using Agentor.Application.Abstractions;
using Agentor.Infrastructure.Athanor;
using Agentor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Agentor.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the default InMemory infrastructure stack.
    /// For Postgres, call <see cref="AddAgentorEfCoreRepository"/> after this.
    /// </summary>
    public static IServiceCollection AddAgentorInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IAgentRunRepository, InMemoryAgentRunRepository>();
        services.AddSingleton<IAgentRunIdempotencyLedger, InMemoryAgentRunIdempotencyLedger>();
        services.AddSingleton<FakeToolExecutor>();
        services.AddSingleton<IToolExecutor>(sp => sp.GetRequiredService<FakeToolExecutor>());
        services.AddSingleton<IToolRegistry>(sp => ToolRegistry.CreateDefault(sp.GetRequiredService<FakeToolExecutor>()));
        services.AddScoped<IPolicyEvaluator, RuntimePolicyEvaluator>();
        services.AddSingleton<IToolExecutionPipeline, ToolExecutionPipeline>();
        services.AddSingleton<IKnowledgeStateClient, FakeKnowledgeStateClient>();

        return services;
    }

    /// <summary>
    /// Replaces the InMemory repository with EF Core and registers <see cref="AgentorDbContext"/>.
    /// Intended for use when persistence mode is Postgres (PR07+).
    /// </summary>
    public static IServiceCollection AddAgentorEfCoreRepository(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDb)
    {
        services.AddDbContext<AgentorDbContext>(configureDb);
        services.AddScoped<IAgentRunRepository, EfCoreAgentRunRepository>();

        foreach (var d in services.Where(x => x.ServiceType == typeof(IAgentRunIdempotencyLedger)).ToList())
        {
            services.Remove(d);
        }

        services.AddScoped<IAgentRunIdempotencyLedger, EfAgentRunIdempotencyLedger>();

        return services;
    }
}
