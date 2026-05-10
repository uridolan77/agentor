using Agentor.Domain.Enums;

namespace Agentor.Domain;

public sealed class AgentRun
{
    private readonly List<AgentStep> _steps = new();
    private readonly List<ExecutionTraceEvent> _trace = new();

    private AgentRun(Guid id, Guid profileId, string agentName, string objective, string traceId, DateTimeOffset startedAt)
    {
        Id = id;
        ProfileId = profileId;
        AgentName = agentName;
        Objective = objective;
        TraceId = traceId;
        StartedAt = startedAt;
        Status = AgentRunStatus.Running;
    }

    public Guid Id { get; }

    public Guid ProfileId { get; }

    public string AgentName { get; }

    public string Objective { get; }

    public string TraceId { get; }

    public AgentRunStatus Status { get; private set; }

    public DateTimeOffset StartedAt { get; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public string? ErrorMessage { get; private set; }

    public IReadOnlyList<AgentStep> Steps => _steps;

    public IReadOnlyList<ExecutionTraceEvent> Trace => _trace;

    public static AgentRun Start(Guid profileId, string agentName, string objective, string traceId, DateTimeOffset now)
    {
        if (profileId == Guid.Empty)
        {
            throw new ArgumentException("Profile id is required.", nameof(profileId));
        }

        if (string.IsNullOrWhiteSpace(agentName))
        {
            throw new ArgumentException("Agent name is required.", nameof(agentName));
        }

        if (string.IsNullOrWhiteSpace(objective))
        {
            throw new ArgumentException("Objective is required.", nameof(objective));
        }

        if (string.IsNullOrWhiteSpace(traceId))
        {
            throw new ArgumentException("Trace id is required.", nameof(traceId));
        }

        var run = new AgentRun(Guid.NewGuid(), profileId, agentName.Trim(), objective.Trim(), traceId.Trim(), now);
        run.RecordTrace(TraceEventKind.RunStarted, "Agent run started.", now, new Dictionary<string, string>
        {
            ["profileId"] = profileId.ToString(),
            ["objective"] = objective.Trim()
        });

        return run;
    }

    public AgentStep StartStep(string name, DateTimeOffset now)
    {
        EnsureRunning();
        var step = AgentStep.Start(Id, _steps.Count + 1, name, now);
        _steps.Add(step);
        RecordTrace(TraceEventKind.StepStarted, $"Step started: {name}", now, new Dictionary<string, string>
        {
            ["stepId"] = step.Id.ToString(),
            ["stepIndex"] = step.Index.ToString()
        });

        return step;
    }

    public void RecordTrace(
        TraceEventKind kind,
        string message,
        DateTimeOffset now,
        IReadOnlyDictionary<string, string>? data = null)
    {
        _trace.Add(new ExecutionTraceEvent(Guid.NewGuid(), Id, kind, message, now, data));
    }

    public void Complete(DateTimeOffset now)
    {
        EnsureRunning();

        if (_steps.Count == 0)
        {
            throw new InvalidOperationException("Cannot complete a run with no steps.");
        }

        Status = AgentRunStatus.Completed;
        CompletedAt = now;
        RecordTrace(TraceEventKind.RunCompleted, "Agent run completed.", now);
    }

    public void Fail(string errorMessage, DateTimeOffset now)
    {
        EnsureRunning();
        Status = AgentRunStatus.Failed;
        ErrorMessage = errorMessage;
        CompletedAt = now;
        RecordTrace(TraceEventKind.RunFailed, "Agent run failed.", now, new Dictionary<string, string>
        {
            ["error"] = errorMessage
        });
    }

    public static AgentRun Reconstitute(
        Guid id,
        Guid profileId,
        string agentName,
        string objective,
        string traceId,
        AgentRunStatus status,
        DateTimeOffset startedAt,
        DateTimeOffset? completedAt,
        string? errorMessage,
        IEnumerable<AgentStep> steps,
        IEnumerable<ExecutionTraceEvent> trace)
    {
        var run = new AgentRun(id, profileId, agentName, objective, traceId, startedAt);
        run.Status = status;
        run.CompletedAt = completedAt;
        run.ErrorMessage = errorMessage;
        run._steps.AddRange(steps);
        run._trace.AddRange(trace);
        return run;
    }

    private void EnsureRunning()
    {
        if (Status != AgentRunStatus.Running)
        {
            throw new InvalidOperationException($"Run is not running. Current status: {Status}");
        }
    }
}

public sealed record AgentRunSummary(
    Guid Id,
    Guid ProfileId,
    string AgentName,
    string TraceId,
    AgentRunStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt);

public sealed record AgentRunListPage(
    IReadOnlyList<AgentRunSummary> Items,
    int TotalCount,
    int Skip,
    int Take);
