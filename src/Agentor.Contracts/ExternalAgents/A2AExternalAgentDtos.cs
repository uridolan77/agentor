namespace Agentor.Contracts.ExternalAgents;

/// <summary>A2A-shaped presentation DTOs only (not Agentor domain).</summary>
public sealed record A2ACapabilityDto(string Id, string Title, string Summary);

public sealed record A2AAgentCardDto(
    string AgentKey,
    string DisplayName,
    string Summary,
    IReadOnlyList<A2ACapabilityDto> Capabilities);

public sealed record A2AInvocationMetadataDto(
    string AgentKey,
    string CapabilityId,
    string RequestFingerprint);
