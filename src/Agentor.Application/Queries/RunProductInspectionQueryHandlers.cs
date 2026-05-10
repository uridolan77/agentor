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

        var ordered = run.Trace
            .OrderBy(e => e.OccurredAt)
            .ThenBy(e => e.Id)
            .Select(e => new RunTimelineEventResponseDto(
                e.Id,
                e.Kind,
                e.Message,
                e.OccurredAt,
                new SortedDictionary<string, string>(new Dictionary<string, string>(e.Data), StringComparer.Ordinal)))
            .ToList();

        var skillInvocations = BuildSkillInvocations(run.Trace.OrderBy(e => e.OccurredAt).ThenBy(e => e.Id).ToList());

        return new RunTimelineResponseDto(run.Id, ordered, skillInvocations);
    }

    private static IReadOnlyList<RunTimelineSkillInvocationDto> BuildSkillInvocations(IReadOnlyList<ExecutionTraceEvent> ordered)
    {
        var stack = new Stack<(int Index, string SkillKey)>();
        var segments = new List<RunTimelineSkillInvocationDto>();

        for (var i = 0; i < ordered.Count; i++)
        {
            var e = ordered[i];
            if (e.Kind == TraceEventKind.SkillInvocationStarted
                && e.Data.TryGetValue("skillKey", out var sk)
                && !string.IsNullOrWhiteSpace(sk))
            {
                stack.Push((i, sk.Trim()));
                continue;
            }

            if (e.Kind == TraceEventKind.SkillInvocationCompleted
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
        var items = new List<PendingHumanReviewItemDto>();
        foreach (var s in page.Items)
        {
            var run = await _repository.GetAsync(s.Id, cancellationToken);
            var reason = run?.ErrorMessage;
            items.Add(new PendingHumanReviewItemDto(s.Id, s.AgentName, s.TraceId, s.StartedAt, s.CompletedAt, reason));
        }

        return new PendingHumanReviewListResponseDto(items);
    }
}
