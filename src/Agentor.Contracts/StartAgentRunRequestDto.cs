namespace Agentor.Contracts;

public sealed record StartAgentRunRequestDto(
    string AgentName,
    string Objective,
    string? TraceId = null);
