namespace Agentor.Contracts;

public sealed record StartAgentRunRequestDto(
    string AgentName,
    string Objective,
    string? TraceId = null,
    Guid? TenantId = null,
    Guid? WorkspaceId = null,
    Guid? ProjectId = null,
    Guid? KnowledgeScopeId = null);
