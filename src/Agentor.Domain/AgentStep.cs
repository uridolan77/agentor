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
        EnsureRunning();
        Status = AgentStepStatus.Completed;
        CompletedAt = now;
    }

    public void Fail(DateTimeOffset now)
    {
        EnsureRunning();
        Status = AgentStepStatus.Failed;
        CompletedAt = now;
    }

    private void EnsureRunning()
    {
        if (Status != AgentStepStatus.Running)
        {
            throw new InvalidOperationException($"Step is not running. Current status: {Status}");
        }
    }
}
