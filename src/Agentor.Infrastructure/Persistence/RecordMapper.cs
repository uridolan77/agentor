using System.Text.Json;
using Agentor.Domain;
using Agentor.Domain.Enums;
using Agentor.Infrastructure.Persistence.Records;

namespace Agentor.Infrastructure.Persistence;

internal static class RecordMapper
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // ── Domain → Records ────────────────────────────────────────────────────

    internal static AgentRunRecord ToRecord(AgentRun run)
    {
        return new AgentRunRecord
        {
            Id = run.Id,
            ProfileId = run.ProfileId,
            AgentName = run.AgentName,
            Objective = run.Objective,
            TraceId = run.TraceId,
            Status = run.Status.ToString(),
            StartedAt = run.StartedAt,
            CompletedAt = run.CompletedAt,
            ErrorMessage = run.ErrorMessage,
            SessionMemoryJson = JsonSerializer.Serialize(run.SessionMemory, JsonOpts),
            Steps = run.Steps.Select(ToRecord).ToList(),
            TraceEvents = run.Trace.Select(ToRecord).ToList()
        };
    }

    private static AgentStepRecord ToRecord(AgentStep step)
    {
        return new AgentStepRecord
        {
            Id = step.Id,
            RunId = step.RunId,
            Index = step.Index,
            Name = step.Name,
            Status = step.Status.ToString(),
            StartedAt = step.StartedAt,
            CompletedAt = step.CompletedAt,
            ToolCalls = step.ToolCalls.Select(ToRecord).ToList(),
            PolicyDecisions = step.PolicyDecisions.Select(ToRecord).ToList()
        };
    }

    private static ToolCallRecord ToRecord(ToolCall toolCall)
    {
        return new ToolCallRecord
        {
            Id = toolCall.Id,
            RunId = toolCall.RunId,
            StepId = toolCall.StepId,
            ToolKey = toolCall.ToolKey,
            Status = toolCall.Status.ToString(),
            InputJson = JsonSerializer.Serialize(toolCall.Input, JsonOpts),
            OutputJson = JsonSerializer.Serialize(toolCall.Output, JsonOpts),
            StartedAt = toolCall.StartedAt,
            CompletedAt = toolCall.CompletedAt,
            ErrorMessage = toolCall.ErrorMessage
        };
    }

    private static PolicyDecisionRecord ToRecord(PolicyDecision decision)
    {
        return new PolicyDecisionRecord
        {
            Id = decision.Id,
            RunId = decision.RunId,
            StepId = decision.StepId,
            Outcome = decision.Outcome.ToString(),
            ReasonCode = decision.ReasonCode,
            Reason = decision.Reason,
            DecidedAt = decision.DecidedAt
        };
    }

    private static TraceEventRecord ToRecord(ExecutionTraceEvent evt)
    {
        return new TraceEventRecord
        {
            Id = evt.Id,
            RunId = evt.RunId,
            Kind = evt.Kind.ToString(),
            Message = evt.Message,
            OccurredAt = evt.OccurredAt,
            DataJson = JsonSerializer.Serialize(evt.Data, JsonOpts)
        };
    }

    // ── Records → Domain ────────────────────────────────────────────────────

    internal static AgentRunSummary ToSummary(AgentRunRecord record)
    {
        return new AgentRunSummary(
            record.Id,
            record.ProfileId,
            record.AgentName,
            record.TraceId,
            Enum.Parse<AgentRunStatus>(record.Status),
            record.StartedAt,
            record.CompletedAt);
    }

    internal static AgentRun ToDomain(AgentRunRecord record)
    {
        var steps = record.Steps
            .OrderBy(s => s.Index)
            .Select(ToDomain)
            .ToList();

        var trace = record.TraceEvents
            .OrderBy(e => e.OccurredAt)
            .Select(ToDomain)
            .ToList();

        var sessionMemory = JsonSerializer.Deserialize<Dictionary<string, string>>(record.SessionMemoryJson, JsonOpts)
                           ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        return AgentRun.Reconstitute(
            record.Id,
            record.ProfileId,
            record.AgentName,
            record.Objective,
            record.TraceId,
            Enum.Parse<AgentRunStatus>(record.Status),
            record.StartedAt,
            record.CompletedAt,
            record.ErrorMessage,
            steps,
            trace,
            sessionMemory);
    }

    private static AgentStep ToDomain(AgentStepRecord record)
    {
        var toolCalls = record.ToolCalls.Select(ToDomain).ToList();
        var policyDecisions = record.PolicyDecisions.Select(ToDomain).ToList();

        return AgentStep.Reconstitute(
            record.Id,
            record.RunId,
            record.Index,
            record.Name,
            Enum.Parse<AgentStepStatus>(record.Status),
            record.StartedAt,
            record.CompletedAt,
            policyDecisions,
            toolCalls);
    }

    private static ToolCall ToDomain(ToolCallRecord record)
    {
        var input = JsonSerializer.Deserialize<Dictionary<string, string>>(record.InputJson, JsonOpts)
                    ?? new Dictionary<string, string>();
        var output = JsonSerializer.Deserialize<Dictionary<string, string>>(record.OutputJson, JsonOpts)
                     ?? new Dictionary<string, string>();

        return ToolCall.Reconstitute(
            record.Id,
            record.RunId,
            record.StepId,
            record.ToolKey,
            Enum.Parse<ToolCallStatus>(record.Status),
            input,
            output,
            record.StartedAt,
            record.CompletedAt,
            record.ErrorMessage);
    }

    private static PolicyDecision ToDomain(PolicyDecisionRecord record)
    {
        return new PolicyDecision(
            record.Id,
            record.RunId,
            record.StepId,
            Enum.Parse<PolicyDecisionOutcome>(record.Outcome),
            record.ReasonCode,
            record.Reason,
            record.DecidedAt);
    }

    private static ExecutionTraceEvent ToDomain(TraceEventRecord record)
    {
        var data = JsonSerializer.Deserialize<Dictionary<string, string>>(record.DataJson, JsonOpts);

        return new ExecutionTraceEvent(
            record.Id,
            record.RunId,
            Enum.Parse<TraceEventKind>(record.Kind),
            record.Message,
            record.OccurredAt,
            data);
    }
}
