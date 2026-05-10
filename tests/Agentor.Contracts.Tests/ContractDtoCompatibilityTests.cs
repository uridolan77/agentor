using System.Collections.Generic;
using System.Text.Json;
using Agentor.Contracts;
using Agentor.Domain.Enums;
using Agentor.Domain.Governance;
using Xunit;

namespace Agentor.Contracts.Tests;

public sealed class ContractDtoCompatibilityTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private static void AssertRoundTrip<T>(T original, Action<T, T>? assert = null)
    {
        var json = JsonSerializer.Serialize(original, JsonOptions);
        var back = JsonSerializer.Deserialize<T>(json, JsonOptions);
        Assert.NotNull(back);
        assert?.Invoke(original, back!);
    }

    [Fact]
    public void StartAgentRunRequestDto_round_trips_json()
    {
        var original = new StartAgentRunRequestDto("Agent", "Goal", "tid", Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var json = JsonSerializer.Serialize(original, JsonOptions);
        var back = JsonSerializer.Deserialize<StartAgentRunRequestDto>(json, JsonOptions);
        Assert.NotNull(back);
        Assert.Equal(original.AgentName, back.AgentName);
        Assert.Equal(original.Objective, back.Objective);
        Assert.Equal(original.TraceId, back.TraceId);
        Assert.Equal(original.TenantId, back.TenantId);
    }

    [Fact]
    public void ApiErrorDto_round_trips_json()
    {
        var original = new ApiErrorDto("Code", "Message", "trace");
        var json = JsonSerializer.Serialize(original, JsonOptions);
        var back = JsonSerializer.Deserialize<ApiErrorDto>(json, JsonOptions);
        Assert.NotNull(back);
        Assert.Equal(original.Error, back.Error);
        Assert.Equal(original.Message, back.Message);
        Assert.Equal(original.TraceId, back.TraceId);
    }

    [Fact]
    public void AgentRunSummaryDto_round_trips_json()
    {
        var id = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var original = new AgentRunSummaryDto(
            id,
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            null,
            null,
            null,
            null,
            "Agent",
            "trace",
            AgentRunStatus.Completed,
            DateTimeOffset.Parse("2026-01-02T03:04:05Z"),
            DateTimeOffset.Parse("2026-01-02T03:05:06Z"));
        var json = JsonSerializer.Serialize(original, JsonOptions);
        var back = JsonSerializer.Deserialize<AgentRunSummaryDto>(json, JsonOptions);
        Assert.NotNull(back);
        Assert.Equal(original.Id, back.Id);
        Assert.Equal(original.Status, back.Status);
    }

    [Fact]
    public void AgentRunDto_round_trips_json()
    {
        var runId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var profileId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var athanorProjectId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var stepId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var policyId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        var toolCallId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        var traceId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var humanId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var actorId = Guid.Parse("77777777-7777-7777-7777-777777777777");

        var step = new AgentStepDto(
            stepId,
            0,
            "step",
            AgentStepStatus.Completed,
            DateTimeOffset.Parse("2026-01-02T03:04:05Z"),
            DateTimeOffset.Parse("2026-01-02T03:04:06Z"),
            new List<PolicyDecisionDto>
            {
                new(policyId, PolicyDecisionOutcome.Allow, "code", "reason", DateTimeOffset.Parse("2026-01-02T03:04:05Z"))
            },
            new List<ToolCallDto>
            {
                new(
                    toolCallId,
                    "tool",
                    ToolCallStatus.Succeeded,
                    new Dictionary<string, string> { ["k"] = "in" },
                    new Dictionary<string, string> { ["o"] = "out" },
                    DateTimeOffset.Parse("2026-01-02T03:04:05Z"),
                    DateTimeOffset.Parse("2026-01-02T03:04:06Z"),
                    null)
            });

        var trace = new List<TraceEventDto>
        {
            new(traceId, TraceEventKind.RunStarted, "msg", DateTimeOffset.Parse("2026-01-02T03:04:05Z"), new Dictionary<string, string> { ["d"] = "v" })
        };

        var human = new List<HumanReviewDecisionDto>
        {
            new(humanId, ReviewDecisionKind.Approve, actorId, DateTimeOffset.Parse("2026-01-02T03:04:05Z"), "n", ReviewResolutionStatus.ResolvedApproved)
        };

        var original = new AgentRunDto(
            runId,
            profileId,
            null,
            null,
            null,
            null,
            athanorProjectId,
            "Agent",
            "Objective",
            "trace-1",
            AgentRunStatus.Completed,
            DateTimeOffset.Parse("2026-01-02T03:04:05Z"),
            DateTimeOffset.Parse("2026-01-02T03:05:06Z"),
            null,
            new List<AgentStepDto> { step },
            trace,
            human);

        AssertRoundTrip(original, (a, b) =>
        {
            Assert.Equal(a.Id, b.Id);
            Assert.Equal(a.Status, b.Status);
            Assert.Single(b.Steps);
            Assert.Single(b.Trace);
            Assert.Single(b.HumanReviewDecisions);
        });
    }

    [Fact]
    public void RunManifestDto_round_trips_json()
    {
        var original = new RunManifestDto(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            "trace",
            AgentRunStatus.Completed,
            DateTimeOffset.Parse("2026-01-02T03:04:05Z"),
            DateTimeOffset.Parse("2026-01-02T03:05:06Z"),
            2,
            3,
            4,
            5,
            1,
            10,
            20,
            1.5m,
            100,
            "openai",
            "gpt",
            "prompt",
            "model",
            0,
            "1.0",
            "hash");
        AssertRoundTrip(original, (a, b) =>
        {
            Assert.Equal(a.RunId, b.RunId);
            Assert.Equal(a.StepCount, b.StepCount);
            Assert.Equal(a.ManifestVersion, b.ManifestVersion);
        });
    }

    [Fact]
    public void RecipeArtifactResponseDto_round_trips_json()
    {
        var step = new RecipeStepResponseDto(
            "s1",
            0,
            RecipeStepKind.Tool,
            "tool",
            null,
            null,
            null,
            FailureHandlingPolicy.FailFast,
            null,
            null,
            null);
        var original = new RecipeArtifactResponseDto(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "R",
            "1.0",
            CoordinationTopology.SequentialPipeline,
            FailureHandlingPolicy.FailFast,
            null,
            new List<RecipeStepResponseDto> { step });
        AssertRoundTrip(original, (_, b) => Assert.Single(b.Steps));
    }

    [Fact]
    public void CreatePlanFromRecipeRequestDto_round_trips_json()
    {
        var original = new CreatePlanFromRecipeRequestDto(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        AssertRoundTrip(original);
    }

    [Fact]
    public void PlanArtifactResponseDto_round_trips_json()
    {
        var planStep = new PlanStepArtifactResponseDto(
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            "s1",
            0,
            RecipeStepKind.Tool,
            "tool",
            null,
            null,
            AgentPlanStepStatus.Completed);
        var original = new PlanArtifactResponseDto(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            "1.0",
            CoordinationTopology.SequentialPipeline,
            FailureHandlingPolicy.FailFast,
            AgentPlanStatus.Completed,
            DateTimeOffset.Parse("2026-01-02T03:04:05Z"),
            new List<PlanStepArtifactResponseDto> { planStep });
        AssertRoundTrip(original, (_, b) => Assert.Single(b.Steps));
    }

    [Fact]
    public void ApplyHumanReviewRequestDto_round_trips_json()
    {
        var original = new ApplyHumanReviewRequestDto(ReviewDecisionKind.RequestChanges, "please fix");
        AssertRoundTrip(original, (a, b) =>
        {
            Assert.Equal(a.Kind, b.Kind);
            Assert.Equal(a.Note, b.Note);
        });
    }

    [Fact]
    public void IntegrationsStatusResponseDto_round_trips_json()
    {
        var original = new IntegrationsStatusResponseDto(
            true,
            new Dictionary<string, IntegrationAdapterStatusDto>
            {
                ["athanor"] = new IntegrationAdapterStatusDto("Fake", true, null)
            },
            new Dictionary<string, TransportResilienceClientDto>
            {
                ["http"] = new TransportResilienceClientDto(false, 0, null)
            });
        AssertRoundTrip(original, (_, b) => Assert.True(b.Ready));
    }

    [Fact]
    public void OperatorDashboardResponseDto_round_trips_json()
    {
        var link = new OperatorDashboardModuleLinkDto("T", "/href");
        var module = new OperatorDashboardModuleDto(
            "M",
            new List<OperatorDashboardModuleLinkDto> { link },
            new Dictionary<string, string> { ["k"] = "v" });
        var original = new OperatorDashboardResponseDto(
            DateTimeOffset.Parse("2026-01-02T03:04:05Z"),
            new Dictionary<string, OperatorDashboardModuleDto> { ["ops"] = module });
        AssertRoundTrip(original, (_, b) => Assert.Single(b.Modules));
    }

    [Fact]
    public void RunTimelineResponseDto_round_trips_json()
    {
        var ev = new RunTimelineEventResponseDto(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            TraceEventKind.RunStarted,
            "m",
            DateTimeOffset.Parse("2026-01-02T03:04:05Z"),
            new Dictionary<string, string> { ["x"] = "y" });
        var skill = new RunTimelineSkillInvocationDto("skill", "1.0", 0, 1, new List<int> { 0 });
        var original = new RunTimelineResponseDto(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            new List<RunTimelineEventResponseDto> { ev },
            new List<RunTimelineSkillInvocationDto> { skill });
        AssertRoundTrip(original, (_, b) =>
        {
            Assert.Single(b.OrderedEvents);
            Assert.Single(b.SkillInvocations);
        });
    }

    [Fact]
    public void RunCoordinationViewResponseDto_round_trips_json()
    {
        var stepView = new RunCoordinationPlanStepViewDto("s1", "Kind", DateTimeOffset.Parse("2026-01-02T03:04:05Z"), AgentPlanStepStatus.Completed);
        var original = new RunCoordinationViewResponseDto(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            CoordinationTopology.SequentialPipeline,
            new List<RunCoordinationPlanStepViewDto> { stepView });
        AssertRoundTrip(original, (_, b) => Assert.Single(b.PlanSteps));
    }
}
