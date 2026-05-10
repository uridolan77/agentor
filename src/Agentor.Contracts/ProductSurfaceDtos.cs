using Agentor.Domain;
using Agentor.Domain.Enums;

namespace Agentor.Contracts;

public sealed record StepGuardRequestDto(
    StepGuardKind Kind,
    string? ReferenceStepId = null,
    string? ExpectedOutputValue = null,
    string? OutputKey = null);

public sealed record RecipeStepRequestDto(
    string StepId,
    int OrderIndex,
    RecipeStepKind Kind,
    string ToolKey,
    StepGuardRequestDto? Guard = null,
    IReadOnlyDictionary<string, string>? InputParameters = null,
    string? OutputKey = null,
    FailureHandlingPolicy OnFailure = FailureHandlingPolicy.FailFast,
    string? CompensationHookId = null,
    string? CompensationDescription = null,
    string? InvokedSkillKey = null,
    string? InvokedSkillVersion = null);

public sealed record CoordinationProfileRefRequestDto(string ProfileKey, string? Version);

public sealed record CreateRecipeRequestDto(
    string Name,
    string Version,
    CoordinationTopology Topology,
    IReadOnlyList<RecipeStepRequestDto> Steps,
    FailureHandlingPolicy PlanFailureHandling = FailureHandlingPolicy.FailFast,
    CoordinationProfileRefRequestDto? ProfileRef = null);

public sealed record RecipeStepResponseDto(
    string StepId,
    int OrderIndex,
    RecipeStepKind Kind,
    string ToolKey,
    StepGuardRequestDto? Guard,
    IReadOnlyDictionary<string, string>? InputParameters,
    string? OutputKey,
    FailureHandlingPolicy OnFailure,
    string? CompensationHookId,
    string? InvokedSkillKey,
    string? InvokedSkillVersion);

public sealed record RecipeArtifactResponseDto(
    Guid Id,
    string Name,
    string Version,
    CoordinationTopology Topology,
    FailureHandlingPolicy PlanFailureHandling,
    CoordinationProfileRefRequestDto? ProfileRef,
    IReadOnlyList<RecipeStepResponseDto> Steps);

public sealed record CreatePlanFromRecipeRequestDto(Guid RecipeId, Guid? PlanId = null);

public sealed record PlanStepArtifactResponseDto(
    Guid Id,
    string SourceStepId,
    int OrderIndex,
    RecipeStepKind Kind,
    string ToolKey,
    string? InvokedSkillKey,
    string? InvokedSkillVersion,
    AgentPlanStepStatus Status);

public sealed record PlanArtifactResponseDto(
    Guid Id,
    Guid RecipeId,
    string RecipeVersion,
    CoordinationTopology Topology,
    FailureHandlingPolicy PlanFailureHandling,
    AgentPlanStatus Status,
    DateTimeOffset CreatedAt,
    IReadOnlyList<PlanStepArtifactResponseDto> Steps);

public sealed record SkillProcedureStepRequestDto(
    string StepId,
    int OrderIndex,
    string Name,
    SkillProcedureStepKind Kind,
    string? ToolKey = null);

public sealed record CreateSkillPackageRequestDto(
    string SkillKey,
    string Version,
    string Name,
    string Purpose,
    IReadOnlyList<SkillProcedureStepRequestDto> ProcedureSteps);

public sealed record SkillPackageSummaryResponseDto(
    Guid Id,
    string SkillKey,
    string Version,
    string Name);

public sealed record SkillPackageDetailResponseDto(
    Guid Id,
    string SkillKey,
    string Version,
    string Name,
    string Purpose,
    IReadOnlyList<SkillProcedureStepRequestDto> ProcedureSteps,
    IReadOnlyList<string> DeclaredToolKeys);

public sealed record PolicyProfileRulesDto(
    IReadOnlyList<string>? AllowedToolKeys = null,
    IReadOnlyList<string>? DeniedToolKeys = null,
    string? MaxAutoApproveRisk = null,
    decimal? MaxDeclaredModelCallCostUnits = null,
    int? MaxDeclaredModelCallLatencyMs = null,
    IReadOnlyList<string>? McpDeniedToolKeys = null,
    IReadOnlyList<string>? ExternalAgentDeniedToolKeys = null);

public sealed record CreatePolicyProfileRequestDto(string Name, PolicyProfileRulesDto Rules);

public sealed record PolicyProfileArtifactResponseDto(
    Guid Id,
    string Name,
    PolicyProfileRulesDto Rules,
    DateTimeOffset CreatedAt);

public sealed record RunTimelineEventResponseDto(
    Guid Id,
    TraceEventKind Kind,
    string Message,
    DateTimeOffset OccurredAt,
    IReadOnlyDictionary<string, string> Data);

public sealed record RunTimelineSkillInvocationDto(
    string SkillKey,
    string? SkillVersion,
    int StartEventIndex,
    int EndEventIndex,
    IReadOnlyList<int> InnerEventIndices);

public sealed record RunTimelineResponseDto(
    Guid RunId,
    IReadOnlyList<RunTimelineEventResponseDto> OrderedEvents,
    IReadOnlyList<RunTimelineSkillInvocationDto> SkillInvocations);

public sealed record RunCoordinationPlanStepViewDto(
    string SourceStepId,
    string? LastPlanEventKind,
    DateTimeOffset? LastPlanEventAt,
    AgentPlanStepStatus? DerivedStatus);

public sealed record RunCoordinationViewResponseDto(
    Guid RunId,
    Guid? PlanId,
    CoordinationTopology? Topology,
    IReadOnlyList<RunCoordinationPlanStepViewDto> PlanSteps);

public sealed record PendingHumanReviewItemDto(
    Guid RunId,
    string AgentName,
    string TraceId,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string? ReviewReason);

public sealed record PendingHumanReviewListResponseDto(IReadOnlyList<PendingHumanReviewItemDto> Items);

public sealed record OperatorDashboardModuleLinkDto(string Title, string Href);

public sealed record OperatorDashboardModuleDto(
    string Title,
    IReadOnlyList<OperatorDashboardModuleLinkDto> Links,
    IReadOnlyDictionary<string, string> Metrics);

public sealed record OperatorDashboardResponseDto(
    DateTimeOffset GeneratedAt,
    IReadOnlyDictionary<string, OperatorDashboardModuleDto> Modules);