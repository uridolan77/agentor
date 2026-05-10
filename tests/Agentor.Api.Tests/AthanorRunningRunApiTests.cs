using System.Net;
using System.Net.Http.Json;
using Agentor.Api.Tests.Support;
using Agentor.Contracts;
using Agentor.Domain;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Agentor.Api.Tests;

/// <summary>
/// Covers Athanor POST paths that require AgentRunStatus.Running.
/// Default POST /api/v1/agent-runs completes the deterministic fake run immediately, so these tests seed a running run via TestAgentRunRepository.
/// </summary>
public sealed class AthanorRunningRunApiTests : IClassFixture<AthanorRunningRunApiFixture>
{
    private readonly AthanorRunningRunApiFixture _factory;

    public AthanorRunningRunApiTests(AthanorRunningRunApiFixture factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostEvidenceProvenance_WhileRunRunning_Returns204NoContent()
    {
        var profileId = Guid.NewGuid();
        var run = AgentRun.Start(
            profileId,
            "Athanor running-run evidence test",
            "Objective.",
            "trace-evidence-running",
            DateTimeOffset.UtcNow);
        _factory.Repository.Seed(run);

        using var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync(
            $"/api/v1/agent-runs/{run.Id}/athanor/evidence-provenance",
            new AttachEvidenceProvenanceRequestDto("running-query"));

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    [Fact]
    public async Task PostCandidate_WhileRunRunning_Returns202Accepted()
    {
        var profileId = Guid.NewGuid();
        var run = AgentRun.Start(
            profileId,
            "Athanor running-run candidate test",
            "Objective.",
            "trace-candidate-running",
            DateTimeOffset.UtcNow);
        _factory.Repository.Seed(run);

        using var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync(
            $"/api/v1/agent-runs/{run.Id}/athanor/candidates",
            new SubmitAthanorCandidateRequestDto("summary while running", "{}"));

        Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);
    }
}