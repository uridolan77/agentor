namespace Agentor.Application.Evaluation;

/// <summary>
/// Named bundle of evaluation cases (PR123).
/// </summary>
public sealed record EvaluationDataset(string Id, IReadOnlyList<EvaluationCase> Cases);
