namespace Agentor.Application.Observability;

/// <summary>
/// Helpers for building log scopes with only operator-safe fields (ids, keys, status, durations — never payloads).
/// </summary>
public static class SafeLogContext
{
    public static IReadOnlyList<KeyValuePair<string, object?>> ForRun(Guid runId, string runTraceId, string? requestTraceId)
    {
        var list = new List<KeyValuePair<string, object?>>
        {
            new(AgentorLogFields.RunId, runId),
            new(AgentorLogFields.RunTraceId, runTraceId),
        };

        if (!string.IsNullOrWhiteSpace(requestTraceId))
        {
            list.Add(new KeyValuePair<string, object?>(AgentorLogFields.RequestTraceId, requestTraceId));
        }

        return list;
    }

    public static IReadOnlyList<KeyValuePair<string, object?>> ForRunId(Guid runId)
    {
        var list = new List<KeyValuePair<string, object?>> { new(AgentorLogFields.RunId, runId) };
        if (AgentorCorrelationContext.Current is { } c)
        {
            list.Add(new KeyValuePair<string, object?>(AgentorLogFields.RequestTraceId, c));
        }

        return list;
    }
}
