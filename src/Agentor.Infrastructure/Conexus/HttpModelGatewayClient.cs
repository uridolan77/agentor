using System.Net.Http.Json;
using Agentor.Application.Abstractions;
using Agentor.Contracts.Conexus;
using Agentor.Infrastructure.Http;
using Agentor.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Agentor.Infrastructure.Conexus;

/// <summary>
/// HTTP adapter for <see cref="IModelGatewayClient"/>.
/// POST v1/model/complete with <see cref="ModelCallRequestDto"/> → <see cref="ModelCallResultDto"/>.
/// </summary>
public sealed class HttpModelGatewayClient(
    IHttpClientFactory httpFactory,
    IOptionsMonitor<AgentorIntegrationsOptions> options)
    : IModelGatewayClient
{
    internal const string HttpClientName = "integration-conexus";

    public async Task<ModelCallResultDto> CompleteAsync(ModelCallRequestDto request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (options.CurrentValue.Conexus.Mode != IntegrationAdapterMode.Http)
        {
            throw new InvalidOperationException("HttpModelGatewayClient requires Agentor:Integrations:Conexus:Mode=Http.");
        }

        using var content = JsonContent.Create(request, options: AgentorHttpJson.Options);
        using var response = await httpFactory.CreateClient(HttpClientName).PostAsync(
            "v1/model/complete",
            content,
            cancellationToken);

        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<ModelCallResultDto>(AgentorHttpJson.Options, cancellationToken);
        return dto ?? throw new InvalidOperationException("Conexus returned an empty model completion body.");
    }
}
