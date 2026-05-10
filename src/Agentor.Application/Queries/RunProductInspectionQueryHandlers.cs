using Agentor.Application.Abstractions;
using Agentor.Contracts;
using Agentor.Domain;
using Agentor.Domain.Enums;

namespace Agentor.Application.Queries;

public sealed class GetRunTimelineQueryHandler
{
    private readonly IAgentRunRepository _repository;

    public GetRunTimelineQueryHandler(IAgentRunRepository repository)
    {
        _repository = repository;
    }

    public async Task<RunTimelineResponseDto?> HandleAsync(Guid runId, CancellationToken cancellationToken)
    {
        var run = await _repository.GetAsync(runId, cancellationToken);
        if (run is null)
        {
            return null;
        }

        var orderedDomain = run.Trace
            .OrderBy(e => e.OccurredAt)
            .ThenBy(e => e.Id)
            .ToList();

        var ordered = orderedDomain
            .Select(e => new RunTimelineEventResponseDto(
                e.Id,
                e.Kind,
                e.Message,
                e.OccurredAt,
                new SortedDictionary<string, string>(
                    new Dictionary<string, string>(e.Data ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
                    StringComparer.Ordinal)))
            .ToList();

        var skillInvocations = BuildSkillInvocations(orderedDomain);

        var groups = BuildTimelineGroups(orderedDomain, skillInvocations);

        return new RunTimelineResponseDto(run.Id, ordered, skillInvocations, groups);
    }

    private static IReadOnlyList<RunTimelineSkillInvocationDto> BuildSkillInvocations(IReadOnlyList<ExecutionTraceEvent> ordered)
    {
        var stack = new Stack<(int Index, string SkillKey)>();
        var segments = new List<RunTimelineSkillInvocationDto>();

        for (var i = 0; i < ordered.Count; i++)
        {
            var e = ordered[i];
            if (e.Kind == TraceEventKind.SkillInvocationStarted
                && e.Data is not null
                && e.Data.TryGetValue("skillKey", out var sk)
                && !string.IsNullOrWhiteSpace(sk))
            {
                stack.Push((i, sk.Trim()));
                continue;
            }

            if (e.Kind == TraceEventKind.SkillInvocationCompleted
                && e.Data is not null
                && e.Data.TryGetValue("skillKey", out var sk2)
                && !string.IsNullOrWhiteSpace(sk2))
            {
                var key = sk2.Trim();
                while (stack.Count > 0)
                {
                    var top = stack.Pop();
                    if (!string.Equals(top.SkillKey, key, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var inner = new List<int>();
                    for (var j = top.Index + 1; j < i; j++)
                    {
                        inner.Add(j);
                    }

                    var version = e.Data.TryGetValue("skillVersion", out var sv) ? sv : null;

                    segments.Add(new RunTimelineSkillInvocationDto(
                        top.SkillKey,
                        version,
                        top.Index,
                        i,
                        inner));
                    break;
                }
            }
        }

        return segments;
    }

    private static IReadOnlyList<RunTimelineGroupV2Dto> BuildTimelineGroups(
        IReadOnlyList<ExecutionTraceEvent> ordered,
        IReadOnlyList<RunTimelineSkillInvocationDto> skills)
    {
        var groups = new List<RunTimelineGroupV2Dto>();

        var openPlan = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < ordered.Count; i++)
        {
            var e = ordered[i];
            if (e.Kind == TraceEventKind.PlanExecutionStepStarted && TrySourceStepId(e, out var sid))
            {
                openPlan[sid] = i;
            }
            else if (IsPlanStepTerminal(e.Kind) && TrySourceStepId(e, out var sid2))
            {
                if (openPlan.TryGetValue(sid2, out var start))
                {
                    groups.Add(new RunTimelineGroupV2Dto(
                        RunTimelineGroupKind.PlanStep,
                        sid2,
                        start,
                        IndicesSpan(start, i)));
                    openPlan.Remove(sid2);
                }
                else
                {
                    groups.Add(new RunTimelineGroupV2Dto(
                        RunTimelineGroupKind.PlanStep,
                        sid2,
                        i,
                        [i]));
                }
            }
        }

        foreach (var kv in openPlan)
        {
            var start = kv.Value;
            groups.Add(new RunTimelineGroupV2Dto(
                RunTimelineGroupKind.PlanStep,
                kv.Key,
                start,
                IndicesSpan(start, ordered.Count - 1)));
        }

        foreach (var s in skills)
        {
            groups.Add(new RunTimelineGroupV2Dto(
                RunTimelineGroupKind.SkillInvocation,
                s.SkillKey,
                s.StartEventIndex,
                IndicesSpan(s.StartEventIndex, s.EndEventIndex)));
        }

        for (var i = 0; i < ordered.Count; i++)
        {
            var k = ordered[i].Kind;
            if (k == TraceEventKind.PolicyEvaluated)
            {
                groups.Add(new RunTimelineGroupV2Dto(RunTimelineGroupKind.PolicyDecision, $"policy:{i}", i, [i]));
            }
            else if (k == TraceEventKind.HumanReviewDecisionRecorded)
            {
                groups.Add(new RunTimelineGroupV2Dto(RunTimelineGroupKind.ReviewDecision, $"review:{i}", i, [i]));
            }
        }

        return groups
            .OrderBy(g => g.AnchorEventIndex)
            .ThenBy(g => g.Kind)
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<int> IndicesSpan(int start, int end)
    {
        var list = new List<int>();
        for (var i = start; i <= end; i++)
        {
            list.Add(i);
        }

        return list;
    }

    private static bool TrySourceStepId(ExecutionTraceEvent e, out string sid)
    {
        sid = "";
        return e.Data is not null
               && e.Data.TryGetValue("sourceStepId", out var raw)
               && !string.IsNullOrWhiteSpace(raw)
               && !string.IsNullOrWhiteSpace(sid = raw.Trim());
    }

    private static bool IsPlanStepTerminal(TraceEventKind kind) =>
        kind is TraceEventKind.PlanExecutionStepCompleted
            or TraceEventKind.PlanExecutionFailed
            or TraceEventKind.PlanExecutionRequiresReview
            or TraceEventKind.PlanStepSkipped;
}

public sealed class GetRunCoordinationViewQueryHandler
{
    private readonly IAgentRunRepository _repository;
    private readonly IManagementPlanStore _plans;

    public GetRunCoordinationViewQueryHandler(IAgentRunRepository repository, IManagementPlanStore plans)
    {
        _repository = repository;
        _plans = plans;
    }

    public async Task<RunCoordinationViewResponseDto?> HandleAsync(Guid runId, CancellationToken cancellationToken)
    {
        var run = await _repository.GetAsync(runId, cancellationToken);
        if (run is null)
        {
            return null;
        }

        Guid? planId = null;
        CoordinationTopology? topology = null;

        foreach (var e in run.Trace.OrderBy(x => x.OccurredAt).ThenBy(x => x.Id))
        {
            if (e.Data.TryGetValue("planId", out var pid) && Guid.TryParse(pid, out var parsed))
            {
                planId = parsed;
                break;
            }
        }

        if (planId is not null && _plans.Get(planId.Value) is { } p)
        {
            topology = p.Topology;
        }

        var stepStates = new Dictionary<string, (string? Kind, DateTimeOffset? At, AgentPlanStepStatus? Status)>(StringComparer.OrdinalIgnoreCase);

        foreach (var e in run.Trace.OrderBy(x => x.OccurredAt).ThenBy(x => x.Id))
        {
            if (!e.Data.TryGetValue("sourceStepId", out var sid) || string.IsNullOrWhiteSpace(sid))
            {
                continue;
            }

            var key = sid.Trim();
            switch (e.Kind)
            {
                case TraceEventKind.PlanExecutionStepStarted:
                    stepStates[key] = (e.Kind.ToString(), e.OccurredAt, AgentPlanStepStatus.Running);
                    break;
                case TraceEventKind.PlanExecutionStepCompleted:
                    stepStates[key] = (e.Kind.ToString(), e.OccurredAt, AgentPlanStepStatus.Completed);
                    break;
                case TraceEventKind.PlanExecutionFailed:
                case TraceEventKind.PlanExecutionRequiresReview:
                    stepStates[key] = (e.Kind.ToString(), e.OccurredAt,
                        e.Kind == TraceEventKind.PlanExecutionRequiresReview
                            ? AgentPlanStepStatus.RequiresReview
                            : AgentPlanStepStatus.Failed);
                    break;
                case TraceEventKind.PlanStepSkipped:
                    stepStates[key] = (e.Kind.ToString(), e.OccurredAt, AgentPlanStepStatus.Skipped);
                    break;
            }
        }

        IReadOnlyList<RunCoordinationPlanStepViewDto> planSteps;
        if (planId is not null && _plans.Get(planId.Value) is { } plan)
        {
            planSteps = plan.Steps
                .OrderBy(s => s.OrderIndex)
                .Select(s =>
                {
                    stepStates.TryGetValue(s.SourceStepId, out var st);
                    return new RunCoordinationPlanStepViewDto(
                        s.SourceStepId,
                        st.Kind,
                        st.At,
                        st.Status ?? s.Status);
                })
                .ToList();
        }
        else
        {
            planSteps = stepStates
                .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .Select(kv => new RunCoordinationPlanStepViewDto(kv.Key, kv.Value.Kind, kv.Value.At, kv.Value.Status))
                .ToList();
        }

        return new RunCoordinationViewResponseDto(run.Id, planId, topology, planSteps);
    }
}

public sealed class ListPendingHumanReviewsQueryHandler
{
    private readonly IAgentRunRepository _repository;

    public ListPendingHumanReviewsQueryHandler(IAgentRunRepository repository)
    {
        _repository = repository;
    }

    public async Task<PendingHumanReviewListResponseDto> HandleAsync(int skip, int take, CancellationToken cancellationToken)
    {
        var page = await _repository.ListSummariesAsync(skip, take, cancellationToken, AgentRunStatus.RequiresReview);
        var items = page.Items
            .Select(s => new PendingHumanReviewItemDto(
                s.Id,
                s.AgentName,
                s.TraceId,
                s.StartedAt,
                s.PausedAt,
                s.ReviewRequestedAt,
                s.ReviewWorkflowStatus,
                s.ErrorMessage))
            .ToList();

        return new PendingHumanReviewListResponseDto(items, page.TotalCount, page.Skip, page.Take);
    }
}
