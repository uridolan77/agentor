using Agentor.Domain.Enums;

namespace Agentor.Domain;

public sealed class ToolCall
{
    private readonly Dictionary<string, string> _input;
    private readonly Dictionary<string, string> _output = new();

    private ToolCall(Guid id, Guid runId, Guid stepId, string toolKey, IReadOnlyDictionary<string, string> input, DateTimeOffset startedAt)
    {
        Id = id;
        RunId = runId;
        StepId = stepId;
        ToolKey = toolKey;
        _input = new Dictionary<string, string>(input, StringComparer.OrdinalIgnoreCase);
        StartedAt = startedAt;
        Status = ToolCallStatus.Running;
    }

    public Guid Id { get; }

    public Guid RunId { get; }

    public Guid StepId { get; }

    public string ToolKey { get; }

    public ToolCallStatus Status { get; private set; }

    public IReadOnlyDictionary<string, string> Input => _input;

    public IReadOnlyDictionary<string, string> Output => _output;

    public DateTimeOffset StartedAt { get; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public string? ErrorMessage { get; private set; }

    public static ToolCall Start(Guid runId, Guid stepId, string toolKey, IReadOnlyDictionary<string, string> input, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(toolKey))
        {
            throw new ArgumentException("Tool key is required.", nameof(toolKey));
        }

        return new ToolCall(Guid.NewGuid(), runId, stepId, toolKey.Trim(), input, now);
    }

    public void Succeed(IReadOnlyDictionary<string, string> output, DateTimeOffset now)
    {
        AgentStateMachine.EnsureToolCallCanMutate(this);
        _output.Clear();

        foreach (var item in output)
        {
            _output[item.Key] = item.Value;
        }

        Status = ToolCallStatus.Succeeded;
        CompletedAt = now;
    }

    public void Fail(string errorMessage, DateTimeOffset now)
    {
        AgentStateMachine.EnsureToolCallCanMutate(this);
        Status = ToolCallStatus.Failed;
        ErrorMessage = errorMessage;
        CompletedAt = now;
    }

    public void Deny(string reason, DateTimeOffset now)
    {
        AgentStateMachine.EnsureToolCallCanMutate(this);
        Status = ToolCallStatus.Denied;
        ErrorMessage = reason;
        CompletedAt = now;
    }

    public void MarkRequiresReview(string reason, DateTimeOffset now)
    {
        AgentStateMachine.EnsureToolCallCanMutate(this);
        Status = ToolCallStatus.RequiresReview;
        ErrorMessage = reason;
        CompletedAt = now;
    }

    /// <summary>Reopens a tool call for execution after an explicit human approval (PR53).</summary>
    public void ResumeAfterHumanReviewApproval(DateTimeOffset now)
    {
        AgentStateMachine.EnsureToolCallCanResumeFromHumanReview(this);
        Status = ToolCallStatus.Running;
        CompletedAt = null;
        ErrorMessage = null;
    }

    /// <summary>Terminal failure after human review rejection while the call was pending review.</summary>
    public void FailAfterHumanReviewRejection(string reason, DateTimeOffset now)
    {
        AgentStateMachine.EnsureToolCallCanResumeFromHumanReview(this);
        Status = ToolCallStatus.Failed;
        ErrorMessage = reason;
        CompletedAt = now;
    }

    public static ToolCall Reconstitute(
        Guid id,
        Guid runId,
        Guid stepId,
        string toolKey,
        ToolCallStatus status,
        IReadOnlyDictionary<string, string> input,
        IReadOnlyDictionary<string, string> output,
        DateTimeOffset startedAt,
        DateTimeOffset? completedAt,
        string? errorMessage)
    {
        var call = new ToolCall(id, runId, stepId, toolKey, input, startedAt);
        call.Status = status;
        call.CompletedAt = completedAt;
        call.ErrorMessage = errorMessage;
        foreach (var kv in output)
        {
            call._output[kv.Key] = kv.Value;
        }

        return call;
    }

}
