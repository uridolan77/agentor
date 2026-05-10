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
    IReadOnlyDictionary<string, string> Output,
    string? ErrorMessage = null);
