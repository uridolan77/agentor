using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Agentor.Application;
using Agentor.Contracts;
using Agentor.Domain.Enums;
using Agentor.Domain.Governance;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Agentor.Api.Tests;

/// <summary>Phase 22 PR106 — HTTP review inbox workflow using runtime policy requiring review for the PR1 fake tool.</summary>
public sealed class ReviewInboxWorkflowApiTests : IClassFixture<ReviewInboxPolicyWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ReviewInboxPolicyWebApplicationFactory _factory;

    public ReviewInboxWorkflowApiTests(ReviewInboxPolicyWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ReviewInbox_EndToEnd_Approve_RemovesFromPending()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Agentor-Actor-Id", Guid.NewGuid().ToString("D"));

        var start = await client.PostAsJsonAsync(
            "/api/v1/agent-runs",
            new StartAgentRunRequestDto("InboxAgent", "Requires review path.", "inbox-e2e-trace"),
            JsonOptions);
        Assert.Equal(HttpStatusCode.Accepted, start.StatusCode);
        var run = await start.Content.ReadFromJsonAsync<AgentRunDto>(JsonOptions);
        Assert.NotNull(run);
        Assert.Equal(AgentRunStatus.RequiresReview, run!.Status);

        var pendingBefore = await client.GetAsync("/api/v1/reviews/pending?skip=0&take=50");
        Assert.Equal(HttpStatusCode.OK, pendingBefore.StatusCode);
        var inboxBefore = await pendingBefore.Content.ReadFromJsonAsync<PendingHumanReviewListResponseDto>(JsonOptions);
        Assert.NotNull(inboxBefore);
        Assert.True(inboxBefore!.TotalCount >= 1);
        Assert.Contains(inboxBefore.Items, i => i.RunId == run.Id);

        var decision = await client.PostAsJsonAsync(
            $"/api/v1/reviews/{run.Id}/decisions",
            new ApplyHumanReviewRequestDto(ReviewDecisionKind.Approve),
            JsonOptions);
        Assert.Equal(HttpStatusCode.OK, decision.StatusCode);
        var updated = await decision.Content.ReadFromJsonAsync<AgentRunDto>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal(AgentRunStatus.Completed, updated!.Status);

        var pendingAfter = await client.GetAsync("/api/v1/reviews/pending?skip=0&take=50");
        var inboxAfter = await pendingAfter.Content.ReadFromJsonAsync<PendingHumanReviewListResponseDto>(JsonOptions);
        Assert.NotNull(inboxAfter);
        Assert.DoesNotContain(inboxAfter!.Items, i => i.RunId == run.Id);
    }
}

public sealed class ReviewInboxPolicyWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Agentor:RuntimePolicy:ActiveProfile:RequiresReviewToolKeys:0"] = WellKnownToolKeys.Pr1FakeTool
            });
        });
    }
}
