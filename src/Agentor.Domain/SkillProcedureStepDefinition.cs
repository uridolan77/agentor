using Agentor.Domain.Enums;

namespace Agentor.Domain;

public sealed record SkillProcedureStepDefinition(
    string StepId,
    int OrderIndex,
    string Name,
    SkillProcedureStepKind Kind,
    string? ToolKey = null);