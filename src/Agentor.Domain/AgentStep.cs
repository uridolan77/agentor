using Agentor.Domain.Enums;

namespace Agentor.Domain;

public sealed class AgentStep
{
    private readonly List<PolicyDecision> _policyDecisions = new();
    private readonly List<ToolCall> _toolCalls = new();

    private AgentStep(Guid id, Guid runId, int index, string name, DateTimeOffset startedAt)
    {
        Id = id;
        RunId = runId;
        Index = index;
        Name = name;
        StartedAt = startedAt;
        Status = AgentStepStatus.Running;
    }

    public Guid Id { get; }

    public Guid RunId { get; }

    public int Index { get; }

    public string Name { get; }

    public AgentStepStatus Status { get; private set; }

    public DateTimeOffset StartedAt { get; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public IReadOnlyList<PolicyDecision> PolicyDecisions => _policyDecisions;

    public IReadOnlyList<ToolCall> ToolCalls => _toolCalls;

    public static AgentStep Start(Guid runId, int index, string name, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Step name is required.", nameof(name));
        }

        return new AgentStep(Guid.NewGuid(), runId, index, name.Trim(), now);
    }

    public void AddPolicyDecision(PolicyDecision decision)
    {
        if (decision.StepId != Id)
        {
            throw new InvalidOperationException("Policy decision belongs to a different step.");
        }

        _policyDecisions.Add(decision);
    }

    public void AddToolCall(ToolCall toolCall)
    {
        if (toolCall.StepId != Id)
        {
            throw new InvalidOperationException("Tool call belongs to a different step.");
        }

        _toolCalls.Add(toolCall);
    }

    public void Complete(DateTimeOffset now)
    {
        AgentStateMachine.EnsureStepCanMutate(this);
        Status = AgentStepStatus.Completed;
        CompletedAt = now;
    }

    public void Fail(DateTimeOffset now)
    {
        AgentStateMachine.EnsureStepCanMutate(this);
        Status = AgentStepStatus.Failed;
        CompletedAt = now;
    }

    public void MarkRequiresReview(DateTimeOffset now)
    {
        AgentStateMachine.EnsureStepCanMutate(this);
        Status = AgentStepStatus.RequiresReview;
        CompletedAt = now;
    }

    public static AgentStep Reconstitute(
        Guid id,
        Guid runId,
        int index,
        string name,
        AgentStepStatus status,
        DateTimeOffset startedAt,
        DateTimeOffset? completedAt,
        IEnumerable<PolicyDecision> policyDecisions,
        IEnumerable<ToolCall> toolCalls)
    {
        var step = new AgentStep(id, runId, index, name, startedAt);
        step.Status = status;
        step.CompletedAt = completedAt;
        step._policyDecisions.AddRange(policyDecisions);
        step._toolCalls.AddRange(toolCalls);
        return step;
    }

}
