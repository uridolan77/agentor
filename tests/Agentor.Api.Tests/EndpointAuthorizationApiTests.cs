using System.Net;
using System.Net.Http.Json;
using Agentor.Api.Security;
using Agentor.Api.Tests.Support;
using Agentor.Application;
using Agentor.Application.Abstractions;
using Agentor.Contracts;
using Agentor.Domain;
using Agentor.Domain.Enums;
using Agentor.Domain.Governance;
using Agentor.Domain.Policy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Agentor.Api.Tests;

public sealed class EndpointAuthorizationApiTests : IClassFixture<EndpointAuthorizationApiFixture>
{
    private readonly EndpointAuthorizationApiFixture _factory;

    public EndpointAuthorizationApiTests(EndpointAuthorizationApiFixture factory)
    {
        _factory = factory;
    }

    private void ResetActor()
    {
        _factory.ActorAccessor.ThrowOnAccess = false;
        _factory.ActorAccessor.CurrentActor = new ActorContext(
            Guid.Parse("eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee"),
            "human",
            ActorRole.HumanOperator);
    }

    [Fact]
    public async Task HumanReview_WithServiceActor_ReturnsForbidden()
    {
        ResetActor();
        var run = BuildRunInRequiresReview(DateTimeOffset.UtcNow);
        _factory.Repository.Seed(run);
        _factory.ActorAccessor.CurrentActor = new ActorContext(
            Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"),
            "service",
            ActorRole.Service);

        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/v1/agent-runs/{run.Id}/human-review",
            new ApplyHumanReviewRequestDto(ReviewDecisionKind.Approve));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListPolicyBundles_WithServiceActor_ReturnsOk()
    {
        ResetActor();
        _factory.ActorAccessor.CurrentActor = new ActorContext(
            Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc"),
            "service",
            ActorRole.Service);

        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/policy-bundles");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreatePolicyBundle_WithServiceActor_ReturnsForbidden()
    {
        ResetActor();
        _factory.ActorAccessor.CurrentActor = new ActorContext(
            Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddddd"),
            "service",
            ActorRole.Service);

        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/policy-bundles", new CreatePolicyBundleRequestDto(
            "blocked",
            "1.0",
            [
                new CreatePolicyRuleDto(
                    PolicyRuleKind.ToolAccess,
                    PolicyRuleScope.Global,
                    PolicyRuleEffect.Allow,
                    WellKnownToolKeys.Pr1FakeTool,
                    null,
                    "allow fake tool")
            ]));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ReviewDecisionAlias_WithServiceActor_ReturnsForbidden()
    {
        ResetActor();
        var run = BuildRunInRequiresReview(DateTimeOffset.UtcNow);
        _factory.Repository.Seed(run);
        _factory.ActorAccessor.CurrentActor = new ActorContext(
            Guid.Parse("abababab-abab-4bab-8bab-abababababab"),
            "service",
            ActorRole.Service);

        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/v1/reviews/{run.Id}/decisions",
            new ApplyHumanReviewRequestDto(ReviewDecisionKind.Approve));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AuditPacketAlias_WithServiceActor_ReturnsOk()
    {
        ResetActor();
        var run = BuildRunInRequiresReview(DateTimeOffset.UtcNow);
        _factory.Repository.Seed(run);
        _factory.ActorAccessor.CurrentActor = new ActorContext(
            Guid.Parse("cdcdcdcd-cdcd-4dcd-8dcd-cdcdcdcdcdcd"),
            "service",
            ActorRole.Service);

        using var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/v1/runs/{run.Id}/audit-packet");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ReviewsPending_WithServiceActor_ReturnsOk()
    {
        ResetActor();
        _factory.ActorAccessor.CurrentActor = new ActorContext(
            Guid.Parse("efefefef-efef-4fef-8fef-efefefefefef"),
            "service",
            ActorRole.Service);

        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/reviews/pending");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HumanReview_WhenActorAccessorThrows_ReturnsUnauthorized()
    {
        ResetActor();
        var run = BuildRunInRequiresReview(DateTimeOffset.UtcNow);
        _factory.Repository.Seed(run);
        _factory.ActorAccessor.ThrowOnAccess = true;

        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/v1/agent-runs/{run.Id}/human-review",
            new ApplyHumanReviewRequestDto(ReviewDecisionKind.Approve));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static AgentRun BuildRunInRequiresReview(DateTimeOffset now)
    {
        var run = AgentRun.Start(Guid.NewGuid(), "EndpointAuth", "review gate", "trace-endpoint-auth", now);
        var step = run.StartStep("Step-Blocked", now);

        var toolCall = ToolCall.Start(run.Id, step.Id, WellKnownToolKeys.Pr1FakeTool, new Dictionary<string, string>(), now);
        step.AddToolCall(toolCall);
        toolCall.MarkRequiresReview("Policy required review", now);
        step.MarkRequiresReview(now);
        run.EnterRequiresReview("Policy required review", now);
        return run;
    }
}

public sealed class EndpointAuthorizationApiFixture : WebApplicationFactory<Program>
{
    public TestAgentRunRepository Repository { get; } = new();

    public MutableActorAccessor ActorAccessor { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAgentRunRepository>();
            services.AddSingleton<IAgentRunRepository>(Repository);

            services.RemoveAll<ICurrentActorAccessor>();
            services.AddSingleton<MutableActorAccessor>(ActorAccessor);
            services.AddSingleton<ICurrentActorAccessor>(sp => sp.GetRequiredService<MutableActorAccessor>());
            services.RemoveAll<IAuthorizationDecisionService>();
            services.AddScoped<IAuthorizationDecisionService, RoleBasedAuthorizationDecisionService>();
        });
    }
}

public sealed class MutableActorAccessor : ICurrentActorAccessor
{
    public bool ThrowOnAccess { get; set; }

    public ActorContext CurrentActor { get; set; } = new(
        Guid.Parse("eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee"),
        "human",
        ActorRole.HumanOperator);

    public ActorContext Current
    {
        get
        {
            if (ThrowOnAccess)
            {
                throw new InvalidOperationException("Injected actor accessor failure for auth path testing.");
            }

            return CurrentActor;
        }
    }
}
