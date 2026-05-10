using Agentor.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Agentor.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAgentorInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IAgentRunRepository, InMemoryAgentRunRepository>();
        services.AddScoped<IPolicyEvaluator, AllowAllPolicyEvaluator>();
        services.AddScoped<IToolExecutor, FakeToolExecutor>();

        return services;
    }
}
