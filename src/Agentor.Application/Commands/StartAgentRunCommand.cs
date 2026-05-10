using System.Collections.Generic;
using Agentor.Domain.Enums;

namespace Agentor.Application.Commands;

public sealed record StartAgentRunCommand(
    string AgentName,
    string Objective,
    string? TraceId = null,
    Guid? TenantId = null,
    Guid? WorkspaceId = null,
    Guid? ProjectId = null,
    Guid? KnowledgeScopeId = null,
    RunExecutionMode? Mode = null,
    Guid? RecipeId = null,
    Guid? PlanId = null,
    string? ToolKey = null,
    string? SkillKey = null,
    IReadOnlyDictionary<string, string>? ToolInput = null);
