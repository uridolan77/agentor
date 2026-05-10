using Agentor.Application.Commands;
using Agentor.Application.Queries;
using Agentor.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Agentor.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddAgentorApplication(this IServiceCollection services)
    {
        services.AddScoped<StartAgentRunHandler>();
        services.AddScoped<AgentRunIdempotencyService>();
        services.AddScoped<GetAgentRunQueryHandler>();
        services.AddScoped<GetRunManifestQueryHandler>();
        services.AddScoped<ListAgentRunsQueryHandler>();
        services.AddScoped<GetAgentRunTraceQueryHandler>();
        services.AddScoped<GetAgentRunStepsQueryHandler>();
        services.AddScoped<GetAgentRunToolCallsQueryHandler>();

        return services;
    }
}
