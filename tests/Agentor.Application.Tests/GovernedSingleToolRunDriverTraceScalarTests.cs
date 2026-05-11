using Agentor.Application;
using Agentor.Application.Abstractions;
using Agentor.Application.Options;
using Agentor.Application.Orchestration;
using Agentor.Domain;
using Agentor.Domain.Enums;
using Agentor.Infrastructure;
using Agentor.Infrastructure.Conexus;
using Agentor.Infrastructure.ExternalAgents;
using Agentor.Infrastructure.Mcp;
using Microsoft.Extensions.Options;
using Xunit;

namespace Agentor.Application.Tests;

public sealed class GovernedSingleToolRunDriverTraceScalarTests
{
    [Fact]
    public async Task SuccessfulRun_ToolCallStartedTrace_HasOnlyScalarIds_NoPayloadBody()
    {
        var repo = new InMemoryAgentRunRepository();
        var clock = new SystemClock();
        var fake = new FakeToolExecutor();
        var registry = ToolRegistry.CreateDefault(fake, new FakeModelGatewayClient(), new FakeMcpRegistryClient(), new FakeA2AExternalAgentClient());
        var policy = new RuntimePolicyEvaluator(registry, clock, Microsoft.Extensions.Options.Options.Create(new RuntimePolicyOptions()));
        var pipeline = new ToolExecutionPipeline(clock, Microsoft.Extensions.Options.Options.Create(new ToolExecutionOptions()));
        var driver = new GovernedSingleToolRunDriver(repo, policy, registry, pipeline, clock);

        var request = new RunOrchestrationRequest(
            AgentName: "Agent",
            Objective: "Objective",
            TraceId: "trace-scalar",
            TenantId: null,
            WorkspaceId: null,
            ProjectId: null,
            KnowledgeScopeId: null,
            Mode: RunExecutionMode.LegacyFakeTool,
            RecipeId: null,
            PlanId: null,
            ToolKey: WellKnownToolKeys.Pr1FakeTool,
            SkillKey: null,
            ToolInput: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["nestedPayload"] = """{"secret":"do-not-emit-in-trace"}"""
            });

        var run = await driver.ExecuteAsync(request, "purpose", "step summary", WellKnownToolKeys.Pr1FakeTool, CancellationToken.None);

        var started = Assert.Single(run.Trace, e => e.Kind == TraceEventKind.ToolCallStarted);
        Assert.NotNull(started.Data);
        Assert.Equal(
            new[] { "stepId", "toolCallId", "toolKey" }.Order(StringComparer.Ordinal),
            started.Data!.Keys.Order(StringComparer.Ordinal));
        foreach (var value in started.Data.Values)
        {
            Assert.DoesNotContain('{', value);
            Assert.DoesNotContain("secret", value, StringComparison.OrdinalIgnoreCase);
        }
    }
}
