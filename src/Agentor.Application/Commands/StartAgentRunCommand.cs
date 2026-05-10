namespace Agentor.Application.Commands;

public sealed record StartAgentRunCommand(
    string AgentName,
    string Objective,
    string? TraceId = null,
    Guid? TenantId = null,
    Guid? WorkspaceId = null,
    Guid? ProjectId = null,
    Guid? KnowledgeScopeId = null);
