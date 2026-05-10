using Agentor.Application;
using Agentor.Application.Abstractions;
using Agentor.Application.Coordination;
using Agentor.Application.Evaluation;
using Agentor.Contracts.ExternalAgents;
using Agentor.Domain.Enums;
using Agentor.Infrastructure;
using Agentor.Infrastructure.Conexus;
using Agentor.Infrastructure.ExternalAgents;
using Agentor.Infrastructure.Mcp;
using Xunit;

namespace Agentor.Application.Tests;

public sealed class ExternalAgentPolicyPreventsHttpInvocationTests
{
    private sealed class CountingExternalAgentClient(IExternalAgentProtocolClient inner) : IExternalAgentProtocolClient
    {
        public int InvokeCount;
        public int ListCount;

        public Task<IReadOnlyList<ExternalAgentCapabilityDto>> ListCapabilitiesAsync(
            ExternalAgentProtocolKind protocolKind,
            CancellationToken cancellationToken = default)
        {
            ListCount++;
            return inner.ListCapabilitiesAsync(protocolKind, cancellationToken);
        }

        public Task<ExternalAgentInvocationResultDto> InvokeAsync(
            ExternalAgentInvocationRequestDto request,
            CancellationToken cancellationToken = default)
        {
            InvokeCount++;
            return inner.InvokeAsync(request, cancellationToken);
        }
    }

    [Fact]
    public async Task External_invoke_policy_deny_does_not_call_protocol_client()
    {
        var clock = new SystemClock();
        var dir = Path.Combine(AppContext.BaseDirectory, "fixtures", "eval");
        var reg = EvaluationFixtureRegistry.Load(Path.Combine(dir, "registry.json"), dir);
        var def = reg.LoadHarnessFixture("external-agent-one-call");
        Assert.True(HarnessProfileMaterializer.TryCreateRunAndPlan(def, CoordinationEvaluationProfile.ExternalAgentTool, clock.UtcNow, out var run, out var plan, out var err), err);

        var counting = new CountingExternalAgentClient(new FakeA2AExternalAgentClient());
        var toolRegistry = ToolRegistry.CreateDefault(
            new FakeToolExecutor(),
            new FakeModelGatewayClient(),
            new FakeMcpRegistryClient(),
            counting);

        var policy = new RuntimePolicyEvaluator(
            toolRegistry,
            clock,
            Microsoft.Extensions.Options.Options.Create(
                new RuntimePolicyOptions
                {
                    ActiveProfile = new PolicyProfileRules
                    {
                        ExternalAgentDeniedToolKeys = [ExternalAgentToolKeys.Invoke],
                    },
                }));

        var executor = AgentorTestComposition.CreateSequentialPlanExecutor(toolRegistry, policy, clock);
        _ = await RunEvaluationHarness.ExecutePlanAsync(executor, run!, plan!, CancellationToken.None);

        Assert.Equal(0, counting.InvokeCount);
        Assert.Contains(run!.Trace, e => e.Kind == TraceEventKind.ExternalAgentInvocationDenied);
    }

    [Fact]
    public async Task External_invoke_requires_review_does_not_call_protocol_client()
    {
        var clock = new SystemClock();
        var dir = Path.Combine(AppContext.BaseDirectory, "fixtures", "eval");
        var reg = EvaluationFixtureRegistry.Load(Path.Combine(dir, "registry.json"), dir);
        var def = reg.LoadHarnessFixture("external-agent-one-call");
        Assert.True(HarnessProfileMaterializer.TryCreateRunAndPlan(def, CoordinationEvaluationProfile.ExternalAgentTool, clock.UtcNow, out var run, out var plan, out var err), err);

        var counting = new CountingExternalAgentClient(new FakeA2AExternalAgentClient());
        var toolRegistry = ToolRegistry.CreateDefault(
            new FakeToolExecutor(),
            new FakeModelGatewayClient(),
            new FakeMcpRegistryClient(),
            counting);

        var policy = new RuntimePolicyEvaluator(
            toolRegistry,
            clock,
            Microsoft.Extensions.Options.Options.Create(
                new RuntimePolicyOptions
                {
                    ActiveProfile = new PolicyProfileRules
                    {
                        RequiresReviewToolKeys = [ExternalAgentToolKeys.Invoke],
                    },
                }));

        var executor = AgentorTestComposition.CreateSequentialPlanExecutor(toolRegistry, policy, clock);
        _ = await RunEvaluationHarness.ExecutePlanAsync(executor, run!, plan!, CancellationToken.None);

        Assert.Equal(0, counting.InvokeCount);
        Assert.Equal(AgentRunStatus.RequiresReview, run!.Status);
        Assert.Contains(run.Trace, e => e.Kind == TraceEventKind.ExternalAgentInvocationRequiresReview);
    }
}
