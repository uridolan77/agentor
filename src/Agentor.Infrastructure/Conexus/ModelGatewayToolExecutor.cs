using System.Globalization;
using Agentor.Application.Abstractions;
using Agentor.Contracts.Conexus;

namespace Agentor.Infrastructure.Conexus;

public sealed class ModelGatewayToolExecutor : IToolExecutor
{
    private readonly IModelGatewayClient _gateway;

    public ModelGatewayToolExecutor(IModelGatewayClient gateway)
    {
        _gateway = gateway;
    }

    public async Task<ToolExecutionResult> ExecuteAsync(
        ToolExecutionRequest request,
        CancellationToken cancellationToken)
    {
        var prompt = ResolvePrompt(request.Input);
        if (string.IsNullOrEmpty(prompt))
        {
            return new ToolExecutionResult(
                false,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                "Model call requires non-empty prompt or objective fallback.");
        }

        var modelId = request.Input.TryGetValue("modelId", out var m) ? m : string.Empty;
        var promptProfileRef = ReadOptionalRef(request.Input, "promptProfileRef");
        var modelProfileRef = ReadOptionalRef(request.Input, "modelProfileRef");
        decimal? declaredCost = null;
        if (request.Input.TryGetValue("declaredCostUnits", out var dc)
            && decimal.TryParse(dc, NumberStyles.Number, CultureInfo.InvariantCulture, out var dcVal))
        {
            declaredCost = dcVal;
        }

        int? declaredLatency = null;
        if (request.Input.TryGetValue("declaredLatencyMs", out var dl)
            && int.TryParse(dl, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dlVal))
        {
            declaredLatency = dlVal;
        }

        var result = await _gateway
            .CompleteAsync(new ModelCallRequestDto(prompt, modelId, promptProfileRef, modelProfileRef, declaredCost, declaredLatency), cancellationToken)
            .ConfigureAwait(false);

        var output = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["completionText"] = result.CompletionText,
            ["providerName"] = result.ProviderName,
            ["modelId"] = result.ModelId,
            ["promptTokens"] = result.PromptTokens.ToString(CultureInfo.InvariantCulture),
            ["completionTokens"] = result.CompletionTokens.ToString(CultureInfo.InvariantCulture),
            ["estimatedCostUnits"] = result.EstimatedCostUnits.ToString(CultureInfo.InvariantCulture),
            ["latencyMs"] = result.LatencyMs.ToString(CultureInfo.InvariantCulture),
            ["toolKey"] = request.ToolKey
        };

        if (!string.IsNullOrWhiteSpace(result.PromptProfileRef))
        {
            output["promptProfileRef"] = result.PromptProfileRef;
        }

        if (!string.IsNullOrWhiteSpace(result.ModelProfileRef))
        {
            output["modelProfileRef"] = result.ModelProfileRef;
        }

        return new ToolExecutionResult(true, output);
    }

    private static string ResolvePrompt(IReadOnlyDictionary<string, string> input)
    {
        if (input.TryGetValue("prompt", out var p) && !string.IsNullOrWhiteSpace(p))
        {
            return p.Trim();
        }

        if (input.TryGetValue("objective", out var o) && !string.IsNullOrWhiteSpace(o))
        {
            return o.Trim();
        }

        return string.Empty;
    }

    private static string? ReadOptionalRef(IReadOnlyDictionary<string, string> input, string key)
    {
        return input.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;
    }
}