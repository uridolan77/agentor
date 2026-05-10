using Agentor.Domain.Enums;

namespace Agentor.Domain;

public sealed class AgentRun
{
    private readonly List<AgentStep> _steps = new();
    private readonly List<ExecutionTraceEvent> _trace = new();
    private readonly Dictionary<string, string> _sessionMemory = new(StringComparer.OrdinalIgnoreCase);

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

    /// <summary>
    /// Bounded run-scoped scratch memory. Not Athanor canon; not durable knowledge.
    /// </summary>
    public IReadOnlyDictionary<string, string> SessionMemory => _sessionMemory;

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

    public SessionMemoryWriteResult TryWriteSessionMemory(string key, string value, SessionMemoryBudget budget, DateTimeOffset now)
    {
        AgentStateMachine.EnsureRunCanMutate(this);
        if (Status != AgentRunStatus.Running)
        {
            return new SessionMemoryWriteResult(SessionMemoryWriteStatus.RejectedNotRunning, "SESSION_MEMORY_NOT_RUNNING");
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            return new SessionMemoryWriteResult(SessionMemoryWriteStatus.RejectedKeyInvalid, "SESSION_MEMORY_KEY_REQUIRED");
        }

        var k = key.Trim();
        if (k.Length > budget.MaxKeyLength)
        {
            return new SessionMemoryWriteResult(SessionMemoryWriteStatus.RejectedKeyTooLarge, "SESSION_MEMORY_KEY_TOO_LARGE");
        }

        if (value.Length > budget.MaxValueLength)
        {
            return new SessionMemoryWriteResult(SessionMemoryWriteStatus.RejectedValueTooLarge, "SESSION_MEMORY_VALUE_TOO_LARGE");
        }

        var isReplace = _sessionMemory.ContainsKey(k);
        if (!isReplace && _sessionMemory.Count >= budget.MaxKeys)
        {
            return new SessionMemoryWriteResult(SessionMemoryWriteStatus.RejectedKeyCount, "SESSION_MEMORY_KEY_LIMIT");
        }

        var projectedTotal = TotalStoredCharactersExcludingKey(k) + value.Length;
        if (projectedTotal > budget.MaxTotalStoredCharacters)
        {
            RecordTrace(
                TraceEventKind.SessionMemoryWriteRejected,
                "Session memory write rejected (total budget).",
                now,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["key"] = k,
                    ["reasonCode"] = "SESSION_MEMORY_TOTAL_BUDGET"
                });
            return new SessionMemoryWriteResult(SessionMemoryWriteStatus.RejectedTotalBudget, "SESSION_MEMORY_TOTAL_BUDGET");
        }

        _sessionMemory[k] = value;
        RecordTrace(
            TraceEventKind.SessionMemoryWriteAccepted,
            "Session memory write accepted (non-canon scratch).",
            now,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["key"] = k,
                ["valueLength"] = value.Length.ToString()
            });

        return new SessionMemoryWriteResult(SessionMemoryWriteStatus.Accepted, null);
    }

    private int TotalStoredCharactersExcludingKey(string keyToReplace)
    {
        var total = 0;
        foreach (var kv in _sessionMemory)
        {
            if (string.Equals(kv.Key, keyToReplace, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            total += kv.Value.Length;
        }

        return total;
    }

    public AgentStep StartStep(string name, DateTimeOffset now)
    {
        AgentStateMachine.EnsureRunCanMutate(this);
        if (Status != AgentRunStatus.Running)
        {
            throw new InvalidOperationException($"Run is not running. Current status: {Status}");
        }

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
        AgentStateMachine.EnsureRunCanComplete(this);

        Status = AgentRunStatus.Completed;
        CompletedAt = now;
        RecordTrace(TraceEventKind.RunCompleted, "Agent run completed.", now);
    }

    public void Fail(string errorMessage, DateTimeOffset now)
    {
        AgentStateMachine.EnsureRunCanFail(this);
        Status = AgentRunStatus.Failed;
        ErrorMessage = errorMessage;
        CompletedAt = now;
        RecordTrace(TraceEventKind.RunFailed, "Agent run failed.", now, new Dictionary<string, string>
        {
            ["error"] = errorMessage
        });
    }

    public void EnterRequiresReview(string reason, DateTimeOffset now)
    {
        AgentStateMachine.EnsureRunCanEnterReview(this);
        Status = AgentRunStatus.RequiresReview;
        ErrorMessage = reason;
        CompletedAt = now;
        RecordTrace(TraceEventKind.RunRequiresReview, "Agent run requires review before continuing.", now, new Dictionary<string, string>
        {
            ["reason"] = reason
        });
    }

    /// <summary>
    /// Records Athanor evidence search result identifiers as provenance inputs. Does not canonize knowledge.
    /// </summary>
    public void AttachAthanorEvidenceSearchProvenance(string query, IReadOnlyList<Guid> evidenceResultIds, DateTimeOffset now)
    {
        AgentStateMachine.EnsureRunCanMutate(this);
        if (Status != AgentRunStatus.Running)
        {
            throw new InvalidOperationException($"Run must be Running to attach Athanor provenance. Current status: {Status}");
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Query is required.", nameof(query));
        }

        RecordTrace(
            TraceEventKind.AthanorEvidenceSearchProvenanceAttached,
            "Athanor evidence search results recorded as provenance input (non-canon).",
            now,
            new Dictionary<string, string>
            {
                ["query"] = query.Trim(),
                ["evidenceResultIds"] = string.Join(',', evidenceResultIds.Select(id => id.ToString("D")))
            });
    }

    /// <summary>Records a non-canon candidate submission envelope for audit and manifest evidence.</summary>
    public void RecordAthanorCandidateSubmission(Guid candidateId, string summary, DateTimeOffset now)
    {
        AgentStateMachine.EnsureRunCanMutate(this);
        if (Status != AgentRunStatus.Running)
        {
            throw new InvalidOperationException($"Run must be Running to record Athanor candidate submission. Current status: {Status}");
        }

        RecordTrace(
            TraceEventKind.AthanorCandidateSubmitted,
            "Candidate knowledge submitted to Athanor (non-canon).",
            now,
            new Dictionary<string, string>
            {
                ["candidateId"] = candidateId.ToString("D"),
                ["summary"] = summary.Trim()
            });
    }

    /// <summary>Records that a review queue item was enqueued in Athanor (human review; not canon).</summary>
    public void RecordAthanorReviewQueued(Guid reviewItemId, Guid candidateId, Guid actorId, DateTimeOffset now)
    {
        AgentStateMachine.EnsureRunCanMutate(this);
        if (Status != AgentRunStatus.Running)
        {
            throw new InvalidOperationException($"Run must be Running to record Athanor review queue activity. Current status: {Status}");
        }

        RecordTrace(
            TraceEventKind.AthanorReviewQueued,
            "Candidate queued for Athanor human review (non-canon).",
            now,
            new Dictionary<string, string>
            {
                ["reviewItemId"] = reviewItemId.ToString("D"),
                ["candidateId"] = candidateId.ToString("D"),
                ["actorId"] = actorId.ToString("D")
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
        IEnumerable<ExecutionTraceEvent> trace,
        IReadOnlyDictionary<string, string>? sessionMemory = null)
    {
        var run = new AgentRun(id, profileId, agentName, objective, traceId, startedAt);
        run.Status = status;
        run.CompletedAt = completedAt;
        run.ErrorMessage = errorMessage;
        run._steps.AddRange(steps);
        run._trace.AddRange(trace);
        if (sessionMemory is not null)
        {
            foreach (var kv in sessionMemory)
            {
                run._sessionMemory[kv.Key] = kv.Value;
            }
        }

        return run;
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
