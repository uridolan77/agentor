using System.Globalization;
using System.Text.Json.Nodes;
using Agentor.Domain;

namespace Agentor.Contracts.Conexus;

/// <summary>
/// Request routed through Conexus (model gateway). Structured JSON body with optional schema metadata.
/// </summary>
public sealed record ModelCallRequestDto(ToolPayload Payload)
{
    public static ModelCallRequestDto FromLegacy(
        string prompt,
        string modelId,
        string? promptProfileRef = null,
        string? modelProfileRef = null,
        decimal? declaredCostUnits = null,
        int? declaredLatencyMs = null)
    {
        var body = new JsonObject
        {
            ["prompt"] = prompt,
            ["modelId"] = modelId
        };

        if (!string.IsNullOrWhiteSpace(promptProfileRef))
        {
            body["promptProfileRef"] = promptProfileRef.Trim();
        }

        if (!string.IsNullOrWhiteSpace(modelProfileRef))
        {
            body["modelProfileRef"] = modelProfileRef.Trim();
        }

        if (declaredCostUnits is not null)
        {
            body["declaredCostUnits"] = declaredCostUnits.Value;
        }

        if (declaredLatencyMs is not null)
        {
            body["declaredLatencyMs"] = declaredLatencyMs.Value;
        }

        return new ModelCallRequestDto(new ToolPayload(body, null, null, null));
    }
}

/// <summary>
/// Completion envelope from Conexus; JSON body plus summary scalars for traces and legacy bridges.
/// </summary>
public sealed record ModelCallResultDto(ToolPayload Payload)
{
    public static ModelCallResultDto FromLegacy(
        string completionText,
        string providerName,
        string modelId,
        int promptTokens,
        int completionTokens,
        decimal estimatedCostUnits,
        int latencyMs,
        string? promptProfileRef = null,
        string? modelProfileRef = null)
    {
        var body = new JsonObject
        {
            ["completionText"] = completionText,
            ["providerName"] = providerName,
            ["modelId"] = modelId,
            ["promptTokens"] = promptTokens,
            ["completionTokens"] = completionTokens,
            ["estimatedCostUnits"] = estimatedCostUnits,
            ["latencyMs"] = latencyMs
        };

        if (!string.IsNullOrWhiteSpace(promptProfileRef))
        {
            body["promptProfileRef"] = promptProfileRef.Trim();
        }

        if (!string.IsNullOrWhiteSpace(modelProfileRef))
        {
            body["modelProfileRef"] = modelProfileRef.Trim();
        }

        var summary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["completionText"] = completionText,
            ["providerName"] = providerName,
            ["modelId"] = modelId,
            ["promptTokens"] = promptTokens.ToString(CultureInfo.InvariantCulture),
            ["completionTokens"] = completionTokens.ToString(CultureInfo.InvariantCulture),
            ["estimatedCostUnits"] = estimatedCostUnits.ToString(CultureInfo.InvariantCulture),
            ["latencyMs"] = latencyMs.ToString(CultureInfo.InvariantCulture)
        };

        if (!string.IsNullOrWhiteSpace(promptProfileRef))
        {
            summary["promptProfileRef"] = promptProfileRef.Trim();
        }

        if (!string.IsNullOrWhiteSpace(modelProfileRef))
        {
            summary["modelProfileRef"] = modelProfileRef.Trim();
        }

        return new ModelCallResultDto(new ToolPayload(body, null, null, summary));
    }
}
