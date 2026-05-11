using Agentor.Api.Security;
using Agentor.Application;
using Agentor.Application.Abstractions;
using Agentor.Api.Tests.Support;
using Agentor.Domain;
using Agentor.Domain.Enums;
using Agentor.Domain.Governance;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Agentor.Api.Tests;

/// <summary>
/// API host with mutable <see cref="ICurrentActorAccessor"/> and seeded runs for Phase 38 authorization matrix tests.
/// </summary>
public sealed class AuthorizationMatrixApiFixture : WebApplicationFactory<Program>
{
    public TestAgentRunRepository Repository { get; } = new();

    public MutableActorAccessor ActorAccessor { get; } = new();

    public Guid CompletedRunId { get; private set; }

    public Guid ReviewRunId { get; private set; }

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

        var now = DateTimeOffset.UtcNow;
        var completed = AgentRun.Start(Guid.NewGuid(), "Matrix", "completed", "matrix-completed", now);
        var step = completed.StartStep("only", now);
        step.Complete(now);
        completed.Complete(now);
        CompletedRunId = completed.Id;
        Repository.Seed(completed);

        var review = BuildRunInRequiresReview(now);
        ReviewRunId = review.Id;
        Repository.Seed(review);
    }

    private static AgentRun BuildRunInRequiresReview(DateTimeOffset now)
    {
        var run = AgentRun.Start(Guid.NewGuid(), "Matrix", "review gate", "matrix-review", now);
        var s = run.StartStep("Step-Blocked", now);
        var toolCall = ToolCall.Start(run.Id, s.Id, WellKnownToolKeys.Pr1FakeTool, new Dictionary<string, string>(), now);
        s.AddToolCall(toolCall);
        toolCall.MarkRequiresReview("Policy required review", now);
        s.MarkRequiresReview(now);
        run.EnterRequiresReview("Policy required review", now);
        return run;
    }
}
