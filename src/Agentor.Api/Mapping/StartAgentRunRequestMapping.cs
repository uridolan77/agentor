using System.Collections.Generic;
using System.Text.Json;
using Agentor.Application.Commands;
using Agentor.Application.RunQueue;
using Agentor.Contracts;
using Agentor.Domain;

namespace Agentor.Api.Mapping;

internal static class StartAgentRunRequestMapping
{
    public static StartAgentRunCommand ToCommand(StartAgentRunRequestDto dto, string? traceIdOverride)
    {
        ToolPayload? structured = null;
        if (dto.ToolInputPayload is not null
            && dto.ToolInputPayload.Value.ValueKind != JsonValueKind.Undefined
            && dto.ToolInputPayload.Value.ValueKind != JsonValueKind.Null)
        {
            structured = ToolPayload.FromPersistedJson(
                dto.ToolInputPayload.Value.GetRawText(),
                RunQueuePayloadSerialization.JsonOptions);
        }

        IReadOnlyDictionary<string, string>? toolInput = null;
        if (structured is null && dto.Input is { Count: > 0 })
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
            toolInput,
            structured);
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
