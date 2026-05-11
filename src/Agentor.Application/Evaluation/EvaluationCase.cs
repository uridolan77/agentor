namespace Agentor.Application.Evaluation;

/// <summary>
/// One harness execution row inside an <see cref="EvaluationDataset"/> (PR123).
/// </summary>
public sealed record EvaluationCase(
    string Id,
    string FixtureId,
    CoordinationEvaluationProfile Profile,
    IReadOnlyList<EvaluationCaseTag> Tags);
