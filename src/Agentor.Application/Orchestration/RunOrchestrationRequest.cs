using System.Collections.Generic;
using Agentor.Domain;
using Agentor.Domain.Enums;

namespace Agentor.Application.Orchestration;

/// <summary>
/// Normalized start-run intent for <see cref="IAgentRunOrchestrator"/>.
/// </summary>
public sealed record RunOrchestrationRequest(
    string AgentName,
    string Objective,
    string? TraceId,
    Guid? TenantId,
    Guid? WorkspaceId,
    Guid? ProjectId,
    Guid? KnowledgeScopeId,
    RunExecutionMode Mode,
    Guid? RecipeId,
    Guid? PlanId,
    string? ToolKey,
    string? SkillKey,
    IReadOnlyDictionary<string, string>? ToolInput,
    ToolPayload? ToolInputPayload = null);
