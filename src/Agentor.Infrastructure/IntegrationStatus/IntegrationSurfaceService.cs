using Agentor.Contracts;
using Agentor.Infrastructure.Athanor;
using Agentor.Infrastructure.Conexus;
using Agentor.Infrastructure.ExternalAgents;
using Agentor.Infrastructure.Mcp;
using Agentor.Infrastructure.Options;
using Agentor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Agentor.Infrastructure.IntegrationStatus;

/// <summary>
/// Builds readiness and status views without leaking configured credentials.
/// </summary>
public sealed class IntegrationSurfaceService(
    IOptionsMonitor<AgentorIntegrationsOptions> integrations,
    IOptionsMonitor<AgentorPersistenceOptions> persistence,
    IHttpClientFactory httpFactory,
    IServiceScopeFactory scopeFactory)
{
    public async Task<IntegrationsStatusResponseDto> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var opts = integrations.CurrentValue;
        var dict = new Dictionary<string, IntegrationAdapterStatusDto>(StringComparer.OrdinalIgnoreCase)
        {
            ["athanor"] = await AdapterStatusAsync(opts.Athanor, HttpKnowledgeStateClient.HttpClientName, cancellationToken),
            ["conexus"] = await AdapterStatusAsync(opts.Conexus, HttpModelGatewayClient.HttpClientName, cancellationToken),
            ["mcp"] = await AdapterStatusAsync(opts.Mcp, HttpMcpRegistryClient.HttpClientName, cancellationToken),
            ["externalAgents"] = await AdapterStatusAsync(
                opts.ExternalAgents,
                HttpExternalAgentProtocolClient.HttpClientName,
                cancellationToken),
        };

        var persistenceReady = await IsPersistenceReadyAsync(cancellationToken);
        var ready = persistenceReady && dict.Values.All(a => a.Ready);
        return new IntegrationsStatusResponseDto(ready, dict);
    }

    public async Task<(bool Ready, string? Reason)> GetReadinessAsync(CancellationToken cancellationToken = default)
    {
        var status = await GetStatusAsync(cancellationToken);
        if (status.Ready)
        {
            return (true, null);
        }

        var reasons = new List<string>();
        var persistOk = await IsPersistenceReadyAsync(cancellationToken);
        if (!persistOk)
        {
            reasons.Add("persistence_unreachable");
        }

        foreach (var kv in status.Integrations)
        {
            if (!kv.Value.Ready)
            {
                reasons.Add($"{kv.Key}:{kv.Value.Detail ?? "not_ready"}");
            }
        }

        return (false, string.Join("; ", reasons));
    }

    private async Task<bool> IsPersistenceReadyAsync(CancellationToken cancellationToken)
    {
        var mode = persistence.CurrentValue.Mode;
        if (!string.Equals(mode, AgentorPersistenceOptions.ModePostgres, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetService<AgentorDbContext>();
        if (db is null)
        {
            return false;
        }

        return await db.Database.CanConnectAsync(cancellationToken);
    }

    private async Task<IntegrationAdapterStatusDto> AdapterStatusAsync(
        IntegrationFamilyOptions family,
        string httpClientName,
        CancellationToken cancellationToken)
    {
        var mode = family.Mode.ToString();
        switch (family.Mode)
        {
            case IntegrationAdapterMode.Fake:
            case IntegrationAdapterMode.Disabled:
                return new IntegrationAdapterStatusDto(mode, Ready: true, Detail: null);

            case IntegrationAdapterMode.Http:
                var probe = await ProbeHttpAsync(family, httpClientName, cancellationToken);
                return new IntegrationAdapterStatusDto(mode, probe.Ok, probe.Detail);

            default:
                return new IntegrationAdapterStatusDto(mode, Ready: false, Detail: "unknown_mode");
        }
    }

    private async Task<(bool Ok, string? Detail)> ProbeHttpAsync(
        IntegrationFamilyOptions family,
        string httpClientName,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = httpFactory.CreateClient(httpClientName);
            if (client.BaseAddress is null)
            {
                return (false, "missing_base_address");
            }

            var path = family.Http?.ReadinessProbeRelativePath?.Trim() ?? string.Empty;
            var uri = string.IsNullOrEmpty(path)
                ? client.BaseAddress
                : new Uri(client.BaseAddress, path);

            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.GetType().Name);
        }
    }
}
