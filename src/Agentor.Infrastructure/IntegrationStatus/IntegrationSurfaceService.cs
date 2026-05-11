using Agentor.Application.Abstractions;
using Agentor.Application.Observability;
using Agentor.Contracts;
using Agentor.Infrastructure.Athanor;
using Agentor.Infrastructure.Conexus;
using Agentor.Infrastructure.ExternalAgents;
using Agentor.Infrastructure.HttpResilience;
using Agentor.Infrastructure.Mcp;
using Agentor.Infrastructure.Options;
using Agentor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Agentor.Infrastructure.IntegrationStatus;

/// <summary>
/// Builds readiness and status views without leaking configured credentials.
/// </summary>
public sealed class IntegrationSurfaceService(
    IOptionsMonitor<AgentorIntegrationsOptions> integrations,
    IOptionsMonitor<AgentorPersistenceOptions> persistence,
    IHttpClientFactory httpFactory,
    IServiceScopeFactory scopeFactory,
    TransportResilienceRegistry transportResilience,
    ILogger<IntegrationSurfaceService> logger,
    IRuntimeMetricsRecorder metrics)
{
    public async Task<IntegrationsStatusResponseDto> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var opts = integrations.CurrentValue;
        var dict = new Dictionary<string, IntegrationAdapterStatusDto>(StringComparer.OrdinalIgnoreCase)
        {
            ["athanor"] = await AdapterStatusAsync("athanor", opts.Athanor, HttpKnowledgeStateClient.HttpClientName, cancellationToken),
            ["conexus"] = await AdapterStatusAsync("conexus", opts.Conexus, HttpModelGatewayClient.HttpClientName, cancellationToken),
            ["mcp"] = await AdapterStatusAsync("mcp", opts.Mcp, HttpMcpRegistryClient.HttpClientName, cancellationToken),
            ["externalAgents"] = await AdapterStatusAsync(
                "externalAgents",
                opts.ExternalAgents,
                HttpExternalAgentProtocolClient.HttpClientName,
                cancellationToken),
        };

        var persistenceReady = await IsPersistenceReadyAsync(cancellationToken);
        var ready = persistenceReady && dict.Values.All(a => a.Ready);
        var resilience = BuildTransportResilienceSnapshot();
        return new IntegrationsStatusResponseDto(ready, dict, resilience);
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

    private IReadOnlyDictionary<string, TransportResilienceClientDto>? BuildTransportResilienceSnapshot()
    {
        var snap = transportResilience.GetSnapshot();
        if (snap.Count == 0)
        {
            return null;
        }

        var dict = new Dictionary<string, TransportResilienceClientDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in snap)
        {
            dict[kv.Key] = new TransportResilienceClientDto(
                kv.Value.CircuitOpen,
                kv.Value.ConsecutiveFailures,
                kv.Value.CircuitOpenUntilUtc);
        }

        return dict;
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
        string integrationName,
        IntegrationFamilyOptions family,
        string httpClientName,
        CancellationToken cancellationToken)
    {
        var mode = family.Mode.ToString();
        switch (family.Mode)
        {
            case IntegrationAdapterMode.Fake:
                return new IntegrationAdapterStatusDto(mode, Ready: true, Detail: null);

            case IntegrationAdapterMode.Disabled:
                return new IntegrationAdapterStatusDto(mode, Ready: true, Detail: "disabled");

            case IntegrationAdapterMode.Http:
                var probe = await ProbeHttpAsync(family, httpClientName, cancellationToken);
                if (!probe.Ok)
                {
                    metrics.RecordIntegrationError(integrationName);
                    var safeDetail = ObservabilityRedaction.SanitizeForLog(probe.Detail ?? "not_ready");
                    logger.LogWarning(
                        AgentorEventIds.IntegrationError,
                        "integration.error {IntegrationName} {Status} {Detail}",
                        integrationName,
                        "not_ready",
                        safeDetail);
                }

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

            if (!response.IsSuccessStatusCode)
            {
                return (false, $"http_{(int)response.StatusCode}");
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.GetType().Name);
        }
    }
}
