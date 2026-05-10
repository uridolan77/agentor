namespace Agentor.Domain;

public sealed class AgentEvaluationResult
{
    public AgentEvaluationResult(
        Guid runId,
        double successScore,
        double safetyScore,
        double latencyMs,
        double estimatedCost,
        bool passesGates)
    {
        RunId = runId;
        SuccessScore = successScore;
        SafetyScore = safetyScore;
        LatencyMs = latencyMs;
        EstimatedCost = estimatedCost;
        PassesGates = passesGates;
    }

    public Guid RunId { get; }

    public double SuccessScore { get; }

    public double SafetyScore { get; }

    public double LatencyMs { get; }

    public double EstimatedCost { get; }

    public bool PassesGates { get; }
}
