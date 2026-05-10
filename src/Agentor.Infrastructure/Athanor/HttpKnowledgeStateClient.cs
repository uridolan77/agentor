using System.Net;
using System.Net.Http.Json;
using Agentor.Application.Abstractions;
using Agentor.Contracts.KnowledgeState;
using Agentor.Infrastructure.Http;
using Agentor.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Agentor.Infrastructure.Athanor;

/// <summary>
/// HTTP adapter for <see cref="IKnowledgeStateClient"/>.
/// Remote JSON contract under <see cref="HttpIntegrationOptions.BaseUrl"/>:
/// GET v1/projects/{{projectId}}/snapshots/latest → CanonicalSnapshotDto | 404
/// GET v1/projects/{{projectId}}/canonical/{{key}} → CanonicalStateEntryDto | 404
/// GET v1/projects/{{projectId}}/evidence/search?query={{q}} → EvidenceSearchResultDto[]
/// POST v1/projects/{{projectId}}/runs/{{agentRunId}}/candidates → CandidateSubmissionResultDto
/// POST v1/projects/{{projectId}}/candidates/{{candidateId}}/review-queue body {{ actorId }} → ReviewQueueResultDto
/// </summary>
public sealed class HttpKnowledgeStateClient(
    IHttpClientFactory httpFactory,
    IOptionsMonitor<AgentorIntegrationsOptions> options)
    : IKnowledgeStateClient
{
    internal const string HttpClientName = "integration-athanor";

    public async Task<CanonicalSnapshotDto?> GetLatestSnapshotAsync(Guid projectId, CancellationToken cancellationToken)
    {
        using var response = await Client().GetAsync(
            $"v1/projects/{projectId}/snapshots/latest",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccess(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<CanonicalSnapshotDto>(AgentorHttpJson.Options, cancellationToken);
    }

    public async Task<CanonicalStateEntryDto?> LookupCanonicalEntryAsync(
        Guid projectId,
        string canonicalKey,
        CancellationToken cancellationToken)
    {
        var keySegment = Uri.EscapeDataString(canonicalKey);
        using var response = await Client().GetAsync(
            $"v1/projects/{projectId}/canonical/{keySegment}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccess(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<CanonicalStateEntryDto>(AgentorHttpJson.Options, cancellationToken);
    }

    public async Task<IReadOnlyList<EvidenceSearchResultDto>> SearchEvidenceAsync(
        Guid projectId,
        string query,
        CancellationToken cancellationToken)
    {
        var qs = Uri.EscapeDataString(query);
        using var response = await Client().GetAsync(
            $"v1/projects/{projectId}/evidence/search?query={qs}",
            cancellationToken);

        await EnsureSuccess(response, cancellationToken);
        var list = await response.Content.ReadFromJsonAsync<List<EvidenceSearchResultDto>>(AgentorHttpJson.Options, cancellationToken);
        return list ?? [];
    }

    public async Task<CandidateSubmissionResultDto> SubmitCandidateAsync(
        Guid projectId,
        Guid agentRunId,
        CandidateKnowledgeSubmissionDto submission,
        CancellationToken cancellationToken)
    {
        using var content = JsonContent.Create(submission, options: AgentorHttpJson.Options);
        using var response = await Client().PostAsync(
            $"v1/projects/{projectId}/runs/{agentRunId}/candidates",
            content,
            cancellationToken);

        await EnsureSuccess(response, cancellationToken);
        var dto = await response.Content.ReadFromJsonAsync<CandidateSubmissionResultDto>(AgentorHttpJson.Options, cancellationToken);
        return dto ?? throw new InvalidOperationException("Athanor candidate submission returned an empty body.");
    }

    public async Task<ReviewQueueResultDto> QueueForReviewAsync(
        Guid projectId,
        Guid candidateId,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        using var content = JsonContent.Create(new { actorId }, options: AgentorHttpJson.Options);
        using var response = await Client().PostAsync(
            $"v1/projects/{projectId}/candidates/{candidateId}/review-queue",
            content,
            cancellationToken);

        await EnsureSuccess(response, cancellationToken);
        var dto = await response.Content.ReadFromJsonAsync<ReviewQueueResultDto>(AgentorHttpJson.Options, cancellationToken);
        return dto ?? throw new InvalidOperationException("Athanor review queue returned an empty body.");
    }

    private HttpClient Client()
    {
        if (options.CurrentValue.Athanor.Mode != IntegrationAdapterMode.Http)
        {
            throw new InvalidOperationException("HttpKnowledgeStateClient requires Agentor:Integrations:Athanor:Mode=Http.");
        }

        return httpFactory.CreateClient(HttpClientName);
    }

    private static async Task EnsureSuccess(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(
            $"Athanor HTTP {(int)response.StatusCode} {response.ReasonPhrase}. Body: {Truncate(body)}");
    }

    private static string Truncate(string s, int max = 512) =>
        s.Length <= max ? s : s[..max] + "…";
}
