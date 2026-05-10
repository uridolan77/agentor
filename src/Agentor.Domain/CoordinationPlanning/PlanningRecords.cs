using Agentor.Domain.Enums;

namespace Agentor.Domain;

/// <summary>Optional reference to a coordination profile (metadata only in Phase 4).</summary>
public sealed record CoordinationProfileRef(string ProfileKey, string? Version);

public sealed record AgentRecipeVersion(string Value)
{
    public static AgentRecipeVersion Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Recipe version is required.", nameof(value));
        }

        return new AgentRecipeVersion(value.Trim());
    }
}

public sealed record StepInputBinding(IReadOnlyDictionary<string, string> Parameters)
{
    public static StepInputBinding Empty { get; } = new StepInputBinding(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
}

public sealed record StepOutputBinding(string OutputKey)
{
    public string NormalizedKey => OutputKey.Trim();
}

public sealed record CompensationHookDefinition(string HookId, string? Description);

public sealed record FailureReason(string Code, string Message, FailureCategory Category);

public sealed record StepFailureSummary(FailureReason Reason, RetryDisposition RetryDisposition);

public sealed record PlanFailureSummary(bool PlanFailed, FailureReason? PrimaryFailure, EscalationDisposition Escalation);

/// <summary>Declarative guard metadata; evaluated later by the coordination runtime.</summary>
public sealed record StepGuardDefinition(
    StepGuardKind Kind,
    string? ReferenceStepId = null,
    string? ExpectedOutputValue = null,
    string? OutputKey = null);
