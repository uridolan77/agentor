using System.Net.Http.Json;
using Agentor.Application.Abstractions;
using Agentor.Contracts.ExternalAgents;
using Agentor.Infrastructure.Http;
using Agentor.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Agentor.Infrastructure.ExternalAgents;

/// <summary>
/// HTTP JSON adapter for <see cref="IExternalAgentProtocolClient"/>.
/// GET v1/capabilities?protocolKind={{int}} → ExternalAgentCapabilityDto[]
/// POST v1/invocations → ExternalAgentInvocationResultDto
/// </summary>
public sealed class HttpExternalAgentProtocolClient(
    IHttpClientFactory httpFactory,
    IOptionsMonitor<AgentorIntegrationsOptions> options)
    : IExternalAgentProtocolClient
{
    internal const string HttpClientName = "integration-external-agents";

    public async Task<IReadOnlyList<ExternalAgentCapabilityDto>> ListCapabilitiesAsync(
        ExternalAgentProtocolKind protocolKind,
        CancellationToken cancellationToken = default)
    {
        EnsureHttpMode();
        var kind = protocolKind == ExternalAgentProtocolKind.Unspecified
            ? ExternalAgentProtocolKind.A2AStyled
            : protocolKind;

        using var response = await Client().GetAsync(
            $"v1/capabilities?protocolKind={(int)kind}",
            cancellationToken);

        await EnsureSuccess(response, cancellationToken);
        var list = await response.Content.ReadFromJsonAsync<List<ExternalAgentCapabilityDto>>(AgentorHttpJson.Options, cancellationToken);
        return list ?? [];
    }

    public async Task<ExternalAgentInvocationResultDto> InvokeAsync(
        ExternalAgentInvocationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        EnsureHttpMode();
        ArgumentNullException.ThrowIfNull(request);

        using var content = JsonContent.Create(request, options: AgentorHttpJson.Options);
        using var response = await Client().PostAsync("v1/invocations", content, cancellationToken);

        await EnsureSuccess(response, cancellationToken);
        var dto = await response.Content.ReadFromJsonAsync<ExternalAgentInvocationResultDto>(AgentorHttpJson.Options, cancellationToken);
        return dto ?? throw new InvalidOperationException("External agent invocation returned an empty body.");
    }

    private static async Task EnsureSuccess(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(
            $"External-agents HTTP {(int)response.StatusCode} {response.ReasonPhrase}. Body: {Truncate(body)}");
    }

    private static string Truncate(string s, int max = 512) =>
        s.Length <= max ? s : s[..max] + "…";

    private HttpClient Client() => httpFactory.CreateClient(HttpClientName);

    private void EnsureHttpMode()
    {
        if (options.CurrentValue.ExternalAgents.Mode != IntegrationAdapterMode.Http)
        {
            throw new InvalidOperationException(
                "HttpExternalAgentProtocolClient requires Agentor:Integrations:ExternalAgents:Mode=Http.");
        }
    }
}
