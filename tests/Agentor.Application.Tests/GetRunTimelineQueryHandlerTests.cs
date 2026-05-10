using Agentor.Application.Queries;
using Agentor.Contracts;
using Agentor.Domain;
using Agentor.Domain.Enums;
using Agentor.Domain.Governance;
using Agentor.Infrastructure;
using Xunit;

namespace Agentor.Application.Tests;

public sealed class GetRunTimelineQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_MultiStepStyleTrace_ProducesOrderedGroups()
    {
        var repo = new InMemoryAgentRunRepository();
        var clock = new SystemClock();
        var now = clock.UtcNow;
        var planId = Guid.NewGuid();

        var run = AgentRun.Start(Guid.NewGuid(), "TimelineAgent", "Objective", "trace-tl", now);
        run.RecordTrace(
            TraceEventKind.PlanExecutionStepStarted,
            "step started",
            now.AddTicks(1),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["sourceStepId"] = "s1",
                ["planId"] = planId.ToString("D")
            });
        run.RecordTrace(
            TraceEventKind.PolicyEvaluated,
            "policy",
            now.AddTicks(2),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["stepId"] = Guid.NewGuid().ToString("D"),
                ["outcome"] = nameof(PolicyDecisionOutcome.RequiresReview)
            });
        run.RecordTrace(
            TraceEventKind.PlanExecutionRequiresReview,
            "needs review",
            now.AddTicks(3),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["sourceStepId"] = "s1"
            });
        run.RecordTrace(
            TraceEventKind.HumanReviewDecisionRecorded,
            "review",
            now.AddTicks(4),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["kind"] = nameof(ReviewDecisionKind.Approve)
            });

        await repo.SaveAsync(run, CancellationToken.None);

        var handler = new GetRunTimelineQueryHandler(repo);
        var dto = await handler.HandleAsync(run.Id, CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(5, dto!.OrderedEvents.Count);
        Assert.NotEmpty(dto.TimelineGroups);

        Assert.Contains(dto.TimelineGroups, g => g.Kind == RunTimelineGroupKind.PlanStep && g.Key == "s1");
        Assert.Contains(dto.TimelineGroups, g => g.Kind == RunTimelineGroupKind.PolicyDecision);
        Assert.Contains(dto.TimelineGroups, g => g.Kind == RunTimelineGroupKind.ReviewDecision);

        for (var i = 1; i < dto.TimelineGroups.Count; i++)
        {
            var prev = dto.TimelineGroups[i - 1];
            var cur = dto.TimelineGroups[i];
            Assert.True(
                cur.AnchorEventIndex > prev.AnchorEventIndex
                || (cur.AnchorEventIndex == prev.AnchorEventIndex && cur.Kind >= prev.Kind),
                "Timeline groups must be deterministically ordered.");
        }
    }
}
