namespace Agentor.Application.Evaluation;

/// <summary>
/// Tags used to select evaluation-case subsets (Phase 32 / PR123).
/// </summary>
public enum EvaluationCaseTag
{
    Smoke,
    Regression,
    Review,
    ExternalAgent,
    Policy,
    Queue
}
