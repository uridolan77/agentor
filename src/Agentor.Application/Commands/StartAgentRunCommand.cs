namespace Agentor.Application.Commands;

public sealed record StartAgentRunCommand(
    string AgentName,
    string Objective,
    string? TraceId = null);
