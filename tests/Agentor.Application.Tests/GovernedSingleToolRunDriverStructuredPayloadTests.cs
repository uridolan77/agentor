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
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Xunit;

namespace Agentor.Application.Tests;

public sealed class GovernedSingleToolRunDriverStructuredPayloadTests
{
    [Fact]
    public async Task StructuredToolInputPayload_RoundTripsThroughToolCall_InputPayloadPreservesBodyAndSummary()
    {
        var repo = new InMemoryAgentRunRepository();
        var clock = new SystemClock();
        var fake = new FakeToolExecutor();
        var registry = ToolRegistry.CreateDefault(fake, new FakeModelGatewayClient(), new FakeMcpRegistryClient(), new FakeA2AExternalAgentClient());
        var policy = new RuntimePolicyEvaluator(registry, clock, Microsoft.Extensions.Options.Options.Create(new RuntimePolicyOptions()));
        var pipeline = new ToolExecutionPipeline(clock, Microsoft.Extensions.Options.Options.Create(new ToolExecutionOptions()));
        var driver = new GovernedSingleToolRunDriver(repo, policy, registry, pipeline, clock);

        var body = JsonNode.Parse("""{"text":"inline-structured"}""")!.AsObject();
        var payload = new ToolPayload(
            body,
            "urn:agentor:inline:test",
            "application/json",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["lane"] = "fast" });

        var request = new RunOrchestrationRequest(
            AgentName: "Agent",
            Objective: "Objective",
            TraceId: "trace-inline-struct",
            TenantId: null,
            WorkspaceId: null,
            ProjectId: null,
            KnowledgeScopeId: null,
            Mode: RunExecutionMode.McpTool,
            RecipeId: null,
            PlanId: null,
            ToolKey: McpToolKeys.Format("demo-server", "echo"),
            SkillKey: null,
            ToolInput: null,
            ToolInputPayload: payload);

        var run = await driver.ExecuteAsync(request, "purpose", "step", McpToolKeys.Format("demo-server", "echo"), CancellationToken.None);

        var toolCall = Assert.Single(run.Steps.SelectMany(s => s.ToolCalls));
        Assert.Equal("inline-structured", toolCall.InputPayload.Body["text"]!.GetValue<string>());
        Assert.Equal("urn:agentor:inline:test", toolCall.InputPayload.SchemaId);
        Assert.Equal("fast", toolCall.InputPayload.Summary["lane"]);
        Assert.Contains("Objective", toolCall.InputPayload.ToPolicyEvaluationDictionary().Keys, StringComparer.OrdinalIgnoreCase);
    }
}
