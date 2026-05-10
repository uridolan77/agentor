using System.Collections.Generic;
using System.Text.Json;
using Agentor.Application.Commands;
using Agentor.Contracts;

namespace Agentor.Api.Mapping;

internal static class StartAgentRunRequestMapping
{
    public static StartAgentRunCommand ToCommand(StartAgentRunRequestDto dto, string? traceIdOverride)
    {
        IReadOnlyDictionary<string, string>? toolInput = null;
        if (dto.Input is { Count: > 0 })
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in dto.Input)
            {
                d[kv.Key] = JsonElementToScalarString(kv.Value);
            }

            toolInput = d;
        }

        return new StartAgentRunCommand(
            dto.AgentName,
            dto.Objective,
            traceIdOverride ?? dto.TraceId,
            dto.TenantId,
            dto.WorkspaceId,
            dto.ProjectId,
            dto.KnowledgeScopeId,
            dto.Mode,
            dto.RecipeId,
            dto.PlanId,
            dto.ToolKey,
            dto.SkillKey,
            toolInput);
    }

    private static string JsonElementToScalarString(JsonElement e) =>
        e.ValueKind switch
        {
            JsonValueKind.String => e.GetString() ?? string.Empty,
            JsonValueKind.Number => e.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => string.Empty,
            _ => e.GetRawText()
        };
}
