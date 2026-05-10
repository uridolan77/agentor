using Agentor.Application.Commands;
using Agentor.Application.Queries;
using Microsoft.Extensions.DependencyInjection;

namespace Agentor.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddAgentorApplication(this IServiceCollection services)
    {
        services.AddScoped<StartAgentRunHandler>();
        services.AddScoped<GetAgentRunQueryHandler>();
        services.AddScoped<GetRunManifestQueryHandler>();

        return services;
    }
}
