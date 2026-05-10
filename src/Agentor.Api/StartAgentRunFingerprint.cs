using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Agentor.Contracts;

namespace Agentor.Api;

public static class StartAgentRunFingerprint
{
    private const char Sep = (char)31;

    public static string Compute(StartAgentRunRequestDto dto, bool traceIdSpecifiedInBody, string? traceIdFromBody)
    {
        var normAgent = string.IsNullOrWhiteSpace(dto.AgentName) ? "PR1 Agent" : dto.AgentName.Trim();
        var traceToken = traceIdSpecifiedInBody && !string.IsNullOrWhiteSpace(traceIdFromBody)
            ? traceIdFromBody.Trim()
            : "";
        var modeTok = dto.Mode?.ToString() ?? "";
        var planTok = dto.PlanId?.ToString("D") ?? "";
        var recipeTok = dto.RecipeId?.ToString("D") ?? "";
        var toolTok = dto.ToolKey ?? "";
        var skillTok = dto.SkillKey ?? "";
        var inputTok = "";
        if (dto.Input is { Count: > 0 } inp)
        {
            var sorted = inp.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => $"{kv.Key}={kv.Value.GetRawText()}");
            inputTok = string.Join(",", sorted);
        }

        var canonical = string.Concat(
            normAgent,
            Sep,
            dto.Objective,
            Sep,
            traceToken,
            Sep,
            modeTok,
            Sep,
            planTok,
            Sep,
            recipeTok,
            Sep,
            toolTok,
            Sep,
            skillTok,
            Sep,
            inputTok);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(hash);
    }
}
