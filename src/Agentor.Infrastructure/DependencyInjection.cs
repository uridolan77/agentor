using System.Linq;
using Agentor.Application.Abstractions;
using Agentor.Infrastructure.Athanor;
using Agentor.Infrastructure.Conexus;
using Agentor.Infrastructure.ExternalAgents;
using Agentor.Infrastructure.IntegrationStatus;
using Agentor.Infrastructure.Mcp;
using Agentor.Infrastructure.Options;
using Agentor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Agentor.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the default infrastructure stack and integration adapters (see <c>Agentor:Integrations</c>).
    /// </summary>
    public static IServiceCollection AddAgentorInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AgentorIntegrationsOptions>()
            .Bind(configuration.GetSection(AgentorIntegrationsOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<AgentorIntegrationsOptions>, AgentorIntegrationsOptionsValidator>();

        RegisterIntegrationHttpClients(services);

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IAgentRunRepository, InMemoryAgentRunRepository>();
        services.AddSingleton<IAgentRunIdempotencyLedger, InMemoryAgentRunIdempotencyLedger>();
        services.AddSingleton<FakeToolExecutor>();
        services.AddSingleton<IToolExecutor>(sp => sp.GetRequiredService<FakeToolExecutor>());

        services.AddSingleton<FakeKnowledgeStateClient>();
        services.AddSingleton<HttpKnowledgeStateClient>();
        services.AddSingleton<DisabledKnowledgeStateClient>();
        services.AddSingleton<IKnowledgeStateClient>(sp =>
        {
            var mode = sp.GetRequiredService<IOptionsMonitor<AgentorIntegrationsOptions>>().CurrentValue.Athanor.Mode;
            return mode switch
            {
                IntegrationAdapterMode.Fake => sp.GetRequiredService<FakeKnowledgeStateClient>(),
                IntegrationAdapterMode.Http => sp.GetRequiredService<HttpKnowledgeStateClient>(),
                IntegrationAdapterMode.Disabled => sp.GetRequiredService<DisabledKnowledgeStateClient>(),
                _ => throw new InvalidOperationException($"Unknown Athanor integration mode: {mode}."),
            };
        });

        services.AddSingleton<FakeModelGatewayClient>();
        services.AddSingleton<HttpModelGatewayClient>();
        services.AddSingleton<DisabledModelGatewayClient>();
        services.AddSingleton<IModelGatewayClient>(sp =>
        {
            var mode = sp.GetRequiredService<IOptionsMonitor<AgentorIntegrationsOptions>>().CurrentValue.Conexus.Mode;
            return mode switch
            {
                IntegrationAdapterMode.Fake => sp.GetRequiredService<FakeModelGatewayClient>(),
                IntegrationAdapterMode.Http => sp.GetRequiredService<HttpModelGatewayClient>(),
                IntegrationAdapterMode.Disabled => sp.GetRequiredService<DisabledModelGatewayClient>(),
                _ => throw new InvalidOperationException($"Unknown Conexus integration mode: {mode}."),
            };
        });

        services.AddSingleton<FakeMcpRegistryClient>();
        services.AddSingleton<HttpMcpRegistryClient>();
        services.AddSingleton<DisabledMcpRegistryClient>();
        services.AddSingleton<IMcpRegistryClient>(sp =>
        {
            var mode = sp.GetRequiredService<IOptionsMonitor<AgentorIntegrationsOptions>>().CurrentValue.Mcp.Mode;
            return mode switch
            {
                IntegrationAdapterMode.Fake => sp.GetRequiredService<FakeMcpRegistryClient>(),
                IntegrationAdapterMode.Http => sp.GetRequiredService<HttpMcpRegistryClient>(),
                IntegrationAdapterMode.Disabled => sp.GetRequiredService<DisabledMcpRegistryClient>(),
                _ => throw new InvalidOperationException($"Unknown MCP integration mode: {mode}."),
            };
        });

        services.AddSingleton<FakeA2AExternalAgentClient>();
        services.AddSingleton<HttpExternalAgentProtocolClient>();
        services.AddSingleton<DisabledExternalAgentProtocolClient>();
        services.AddSingleton<IExternalAgentProtocolClient>(sp =>
        {
            var mode = sp.GetRequiredService<IOptionsMonitor<AgentorIntegrationsOptions>>().CurrentValue.ExternalAgents.Mode;
            return mode switch
            {
                IntegrationAdapterMode.Fake => sp.GetRequiredService<FakeA2AExternalAgentClient>(),
                IntegrationAdapterMode.Http => sp.GetRequiredService<HttpExternalAgentProtocolClient>(),
                IntegrationAdapterMode.Disabled => sp.GetRequiredService<DisabledExternalAgentProtocolClient>(),
                _ => throw new InvalidOperationException($"Unknown external agent integration mode: {mode}."),
            };
        });

        services.AddSingleton<IToolRegistry>(sp => ToolRegistry.CreateDefault(
            sp.GetRequiredService<FakeToolExecutor>(),
            sp.GetRequiredService<IModelGatewayClient>(),
            sp.GetRequiredService<IMcpRegistryClient>(),
            sp.GetRequiredService<IExternalAgentProtocolClient>()));
        services.AddScoped<IPolicyEvaluator, RuntimePolicyEvaluator>();
        services.AddSingleton<IToolExecutionPipeline, ToolExecutionPipeline>();
        services.AddSingleton<InMemorySkillPackageCatalog>();
        services.AddSingleton<ISkillPackageCatalog>(sp => sp.GetRequiredService<InMemorySkillPackageCatalog>());
        services.AddSingleton<IntegrationSurfaceService>();

        return services;
    }

    private static void RegisterIntegrationHttpClients(IServiceCollection services)
    {
        services.AddHttpClient(HttpKnowledgeStateClient.HttpClientName)
            .ConfigureHttpClient((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptionsMonitor<AgentorIntegrationsOptions>>();
                ApplyHttpOptions(client, opts.CurrentValue.Athanor.Http);
            });

        services.AddHttpClient(HttpModelGatewayClient.HttpClientName)
            .ConfigureHttpClient((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptionsMonitor<AgentorIntegrationsOptions>>();
                ApplyHttpOptions(client, opts.CurrentValue.Conexus.Http);
            });

        services.AddHttpClient(HttpMcpRegistryClient.HttpClientName)
            .ConfigureHttpClient((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptionsMonitor<AgentorIntegrationsOptions>>();
                ApplyHttpOptions(client, opts.CurrentValue.Mcp.Http);
            });

        services.AddHttpClient(HttpExternalAgentProtocolClient.HttpClientName)
            .ConfigureHttpClient((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptionsMonitor<AgentorIntegrationsOptions>>();
                ApplyHttpOptions(client, opts.CurrentValue.ExternalAgents.Http);
            });
    }

    private static void ApplyHttpOptions(HttpClient client, HttpIntegrationOptions? http)
    {
        if (http is null || string.IsNullOrWhiteSpace(http.BaseUrl))
        {
            return;
        }

        client.BaseAddress = new Uri(http.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
        client.Timeout = TimeSpan.FromSeconds(Math.Clamp(http.TimeoutSeconds, 1, 600));

        foreach (var kv in http.DefaultHeaders ?? [])
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation(kv.Key, kv.Value);
        }
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
