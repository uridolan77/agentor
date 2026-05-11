using Agentor.Domain.Enums;

namespace Agentor.Domain;

public sealed class ToolCall
{
    private ToolPayload _inputPayload;
    private ToolPayload _outputPayload = ToolPayload.Empty;

    private ToolCall(Guid id, Guid runId, Guid stepId, string toolKey, ToolPayload inputPayload, DateTimeOffset startedAt)
    {
        Id = id;
        RunId = runId;
        StepId = stepId;
        ToolKey = toolKey;
        _inputPayload = inputPayload;
        StartedAt = startedAt;
        Status = ToolCallStatus.Running;
    }

    public Guid Id { get; }

    public Guid RunId { get; }

    public Guid StepId { get; }

    public string ToolKey { get; }

    public ToolCallStatus Status { get; private set; }

    public ToolPayload InputPayload => _inputPayload;

    public ToolPayload OutputPayload => _outputPayload;

    /// <summary>Stable flat summary for legacy surfaces (DTOs, traces referencing scalar metadata).</summary>
    public IReadOnlyDictionary<string, string> Input => _inputPayload.ToLegacySummary();

    /// <summary>Stable flat summary for legacy surfaces.</summary>
    public IReadOnlyDictionary<string, string> Output => _outputPayload.ToLegacySummary();

    public DateTimeOffset StartedAt { get; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public string? ErrorMessage { get; private set; }

    public static ToolCall Start(Guid runId, Guid stepId, string toolKey, IReadOnlyDictionary<string, string> input, DateTimeOffset now) =>
        Start(runId, stepId, toolKey, ToolPayload.FromLegacyDictionary(input), now);

    public static ToolCall Start(Guid runId, Guid stepId, string toolKey, ToolPayload inputPayload, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(toolKey))
        {
            throw new ArgumentException("Tool key is required.", nameof(toolKey));
        }

        return new ToolCall(Guid.NewGuid(), runId, stepId, toolKey.Trim(), inputPayload, now);
    }

    public void Succeed(IReadOnlyDictionary<string, string> output, DateTimeOffset now) =>
        Succeed(ToolPayload.FromLegacyDictionary(output), now);

    public void Succeed(ToolPayload outputPayload, DateTimeOffset now)
    {
        AgentStateMachine.EnsureToolCallCanMutate(this);
        _outputPayload = outputPayload;

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
        string? errorMessage) =>
        Reconstitute(
            id,
            runId,
            stepId,
            toolKey,
            status,
            ToolPayload.FromLegacyDictionary(input),
            ToolPayload.FromLegacyDictionary(output),
            startedAt,
            completedAt,
            errorMessage);

    public static ToolCall Reconstitute(
        Guid id,
        Guid runId,
        Guid stepId,
        string toolKey,
        ToolCallStatus status,
        ToolPayload inputPayload,
        ToolPayload outputPayload,
        DateTimeOffset startedAt,
        DateTimeOffset? completedAt,
        string? errorMessage)
    {
        var call = new ToolCall(id, runId, stepId, toolKey, inputPayload, startedAt);
        call.Status = status;
        call.CompletedAt = completedAt;
        call.ErrorMessage = errorMessage;
        call._outputPayload = outputPayload;

        return call;
    }

}
