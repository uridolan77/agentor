namespace Agentor.Domain;

public sealed record PlanValidationIssue(string Code, string Message, string? StepId = null);

public sealed class PlanValidationResult
{
    private PlanValidationResult(IReadOnlyList<PlanValidationIssue> issues)
    {
        Issues = issues;
    }

    public IReadOnlyList<PlanValidationIssue> Issues { get; }

    public bool IsValid => Issues.Count == 0;

    public static PlanValidationResult Success { get; } = new PlanValidationResult([]);

    public static PlanValidationResult FromIssues(IEnumerable<PlanValidationIssue> issues)
    {
        var list = issues.ToList();
        return list.Count == 0 ? Success : new PlanValidationResult(list);
    }
}
