using Agentor.Domain;

namespace Agentor.Contracts.ExternalAgents;

public enum ExternalAgentProtocolKind
{
    Unspecified = 0,
    GenericFake = 1,
    A2AStyled = 2,
}

public enum ExternalAgentInvocationStatus
{
    Succeeded = 0,
    Failed = 1,
}

public sealed record ExternalAgentCapabilityDto(
    ExternalAgentProtocolKind ProtocolKind,
    string AgentKey,
    string CapabilityKey,
    string Summary);

public sealed record ExternalAgentInvocationRequestDto(
    ExternalAgentProtocolKind ProtocolKind,
    string AgentKey,
    string CapabilityKey,
    ToolPayload Arguments);

public sealed record ExternalAgentInvocationResultDto(
    ExternalAgentInvocationStatus Status,
    ToolPayload? OutputPayload,
    string? ErrorMessage,
    bool IsNonCanonEvidence);
