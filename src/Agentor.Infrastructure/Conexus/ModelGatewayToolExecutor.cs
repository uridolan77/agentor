using System.Globalization;
using Agentor.Application.Abstractions;
using Agentor.Contracts.Conexus;
using Agentor.Domain;

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
        var flat = request.Input.ToPolicyEvaluationDictionary();
        var prompt = ResolvePrompt(flat);
        if (string.IsNullOrEmpty(prompt))
        {
            return new ToolExecutionResult(
                false,
                ToolPayload.Empty,
                "Model call requires non-empty prompt or objective fallback.");
        }

        var modelId = flat.TryGetValue("modelId", out var m) ? m : string.Empty;
        var promptProfileRef = ReadOptionalRef(flat, "promptProfileRef");
        var modelProfileRef = ReadOptionalRef(flat, "modelProfileRef");
        decimal? declaredCost = null;
        if (flat.TryGetValue("declaredCostUnits", out var dc)
            && decimal.TryParse(dc, NumberStyles.Number, CultureInfo.InvariantCulture, out var dcVal))
        {
            declaredCost = dcVal;
        }

        int? declaredLatency = null;
        if (flat.TryGetValue("declaredLatencyMs", out var dl)
            && int.TryParse(dl, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dlVal))
        {
            declaredLatency = dlVal;
        }

        var gatewayRequest = ModelCallRequestDto.FromLegacy(prompt, modelId, promptProfileRef, modelProfileRef, declaredCost, declaredLatency);

        var result = await _gateway
            .CompleteAsync(gatewayRequest, cancellationToken)
            .ConfigureAwait(false);

        var rflat = result.Payload.ToPolicyEvaluationDictionary();

        static string Pick(IReadOnlyDictionary<string, string> d, string key, string fallback) =>
            d.TryGetValue(key, out var v) ? v : fallback;

        var output = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["completionText"] = Pick(rflat, "completionText", string.Empty),
            ["providerName"] = Pick(rflat, "providerName", string.Empty),
            ["modelId"] = Pick(rflat, "modelId", string.Empty),
            ["promptTokens"] = Pick(rflat, "promptTokens", "0"),
            ["completionTokens"] = Pick(rflat, "completionTokens", "0"),
            ["estimatedCostUnits"] = Pick(rflat, "estimatedCostUnits", "0"),
            ["latencyMs"] = Pick(rflat, "latencyMs", "0"),
            ["toolKey"] = request.ToolKey
        };

        if (rflat.TryGetValue("promptProfileRef", out var ppr) && !string.IsNullOrWhiteSpace(ppr))
        {
            output["promptProfileRef"] = ppr;
        }

        if (rflat.TryGetValue("modelProfileRef", out var mpr) && !string.IsNullOrWhiteSpace(mpr))
        {
            output["modelProfileRef"] = mpr;
        }

        return new ToolExecutionResult(true, new ToolPayload(result.Payload.Body, result.Payload.SchemaId, result.Payload.ContentType, output));
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
