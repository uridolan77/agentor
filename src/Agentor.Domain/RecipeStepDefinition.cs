using Agentor.Domain.Enums;

namespace Agentor.Domain;

public sealed record RecipeStepDefinition(
    string StepId,
    int OrderIndex,
    RecipeStepKind Kind,
    string ToolKey,
    StepGuardDefinition? Guard = null,
    StepInputBinding? InputBinding = null,
    StepOutputBinding? OutputBinding = null,
    FailureHandlingPolicy OnFailure = FailureHandlingPolicy.FailFast,
    CompensationHookDefinition? Compensation = null);
