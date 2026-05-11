using Agentor.Domain;
using Agentor.Domain.Enums;

namespace Agentor.Application.Mcp;

public sealed record McpServerDescriptor(string Id, string DisplayName);

public sealed record McpToolDescriptor(
    string ServerId,
    string Name,
    string Description,
    ToolRiskLevel NominalRisk);

public sealed record McpToolInvocationResult(
    bool Success,
    ToolPayload Output,
    string? ErrorMessage = null);
