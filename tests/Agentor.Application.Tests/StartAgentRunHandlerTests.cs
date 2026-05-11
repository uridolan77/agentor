using Agentor.Application;
using Agentor.Application.Abstractions;
using Agentor.Application.Commands;
using Agentor.Domain;
using Agentor.Domain.Enums;
using Agentor.Infrastructure;
using Agentor.Infrastructure.Conexus;
using Agentor.Infrastructure.Mcp;
using Agentor.Infrastructure.ExternalAgents;
using Microsoft.Extensions.Options;
using Xunit;

namespace Agentor.Application.Tests;

public sealed class StartAgentRunHandlerTests
{
    private sealed class CountingExecutor : IToolExecutor
    {
        public int Invocations;

        public Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request, CancellationToken cancellationToken)
        {
            Invocations++;
            return Task.FromResult(new ToolExecutionResult(true, ToolPayload.FromLegacyDictionary(new Dictionary<string, string>())));
        }
    }

    [Fact]
    public async Task HandleAsync_CreatesCompletedRun_WithPolicyToolAndTrace()
    {
        var clock = new SystemClock();
        var repository = new InMemoryAgentRunRepository();
        var fake = new FakeToolExecutor();
        var registry = ToolRegistry.CreateDefault(fake, new FakeModelGatewayClient(), new FakeMcpRegistryClient(), new FakeA2AExternalAgentClient());
        var policy = new RuntimePolicyEvaluator(registry, clock, Microsoft.Extensions.Options.Options.Create(new RuntimePolicyOptions()));
        var handler = AgentorTestComposition.CreateStartAgentRunHandler(repository, policy, registry, clock);

        var run = await handler.HandleAsync(
            new StartAgentRunCommand("PR1 Agent", "Prove the runtime kernel.", "test-trace"),
            CancellationToken.None);

        Assert.Equal(AgentRunStatus.Completed, run.Status);
        Assert.Single(run.Steps);
        Assert.Single(run.Steps[0].PolicyDecisions);
        Assert.Single(run.Steps[0].ToolCalls);
        Assert.True(run.Trace.Count >= 9);

        var saved = await repository.GetAsync(run.Id, CancellationToken.None);
        Assert.NotNull(saved);
    }

    [Fact]
    public async Task HandleAsync_WhenToolNotRegistered_FailsWithoutExecutor()
    {
        var clock = new SystemClock();
        var repository = new InMemoryAgentRunRepository();
        var registry = new ToolRegistry();
        var policy = new RuntimePolicyEvaluator(registry, clock, Microsoft.Extensions.Options.Options.Create(new RuntimePolicyOptions()));
        var handler = AgentorTestComposition.CreateStartAgentRunHandler(repository, policy, registry, clock);

        var run = await handler.HandleAsync(
            new StartAgentRunCommand("PR1 Agent", "No registered tool.", "no-tool-trace"),
            CancellationToken.None);

        Assert.Equal(AgentRunStatus.Failed, run.Status);
        Assert.Empty(run.Steps[0].ToolCalls);
    }

    [Fact]
    public async Task HandleAsync_WhenPolicyDenies_DoesNotInvokeExecutor()
    {
        var clock = new SystemClock();
        var repository = new InMemoryAgentRunRepository();
        var counting = new CountingExecutor();
        var registry = new ToolRegistry();
        registry.Register(
            new ToolDefinition(WellKnownToolKeys.Pr1FakeTool, "t", "d", ToolRiskLevel.Low),
            counting);
        var policy = new RuntimePolicyEvaluator(
            registry,
            clock,
            Microsoft.Extensions.Options.Options.Create(new RuntimePolicyOptions { DeniedToolKeys = [WellKnownToolKeys.Pr1FakeTool] }));
        var handler = AgentorTestComposition.CreateStartAgentRunHandler(repository, policy, registry, clock);

        var run = await handler.HandleAsync(
            new StartAgentRunCommand("PR1 Agent", "Denied.", "deny-trace"),
            CancellationToken.None);

        Assert.Equal(AgentRunStatus.Failed, run.Status);
        Assert.Equal(PolicyDecisionOutcome.Deny, run.Steps[0].PolicyDecisions[0].Outcome);
        Assert.Equal(0, counting.Invocations);
        Assert.DoesNotContain(run.Trace, e => e.Kind == TraceEventKind.ToolExecutionAttemptStarted);
    }

    [Fact]
    public async Task HandleAsync_WhenPolicyRequiresReview_DoesNotInvokeExecutor()
    {
        var clock = new SystemClock();
        var repository = new InMemoryAgentRunRepository();
        var counting = new CountingExecutor();
        var registry = new ToolRegistry();
        registry.Register(
            new ToolDefinition(WellKnownToolKeys.Pr1FakeTool, "t", "d", ToolRiskLevel.High),
            counting);
        var policy = new RuntimePolicyEvaluator(
            registry,
            clock,
            Microsoft.Extensions.Options.Options.Create(new RuntimePolicyOptions { MaxAutoApproveRisk = nameof(ToolRiskLevel.Low) }));
        var handler = AgentorTestComposition.CreateStartAgentRunHandler(repository, policy, registry, clock);

        var run = await handler.HandleAsync(
            new StartAgentRunCommand("PR1 Agent", "Needs review.", "review-trace"),
            CancellationToken.None);

        Assert.Equal(AgentRunStatus.RequiresReview, run.Status);
        Assert.Equal(PolicyDecisionOutcome.RequiresReview, run.Steps[0].PolicyDecisions[0].Outcome);
        Assert.Equal(AgentStepStatus.RequiresReview, run.Steps[0].Status);
        Assert.Equal(ToolCallStatus.RequiresReview, run.Steps[0].ToolCalls[0].Status);
        Assert.Equal(0, counting.Invocations);
        Assert.DoesNotContain(run.Trace, e => e.Kind == TraceEventKind.ToolExecutionAttemptStarted);
    }
}
