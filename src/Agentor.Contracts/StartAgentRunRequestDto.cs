using System.Text.Json;
using Agentor.Domain.Enums;

namespace Agentor.Contracts;

public sealed record StartAgentRunRequestDto(
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
    Dictionary<string, JsonElement>? Input = null,
    JsonElement? ToolInputPayload = null);
