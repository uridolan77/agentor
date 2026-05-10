using System.Globalization;
using Agentor.Application;
using Agentor.Domain;
using Agentor.Domain.Enums;

namespace Agentor.Application.Manifest;

/// <summary>
/// Maps gateway-shaped tool outputs into <see cref="RunManifestModelTelemetry"/> for manifests.
/// Conexus/tool-key coupling lives here, not in Domain.
/// </summary>
public static class ModelCallTelemetryAggregator
{
    public static RunManifestModelTelemetry Aggregate(AgentRun run)
    {
        long pTok = 0;
        long cTok = 0;
        decimal cost = 0;
        long lat = 0;
        var count = 0;
        string? firstProv = null;
        string? firstModel = null;
        string? firstPRef = null;
        string? firstMRef = null;

        foreach (var step in run.Steps)
        {
            foreach (var call in step.ToolCalls)
            {
                if (!string.Equals(call.ToolKey, WellKnownToolKeys.ConexusModelComplete, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (call.Status != ToolCallStatus.Succeeded)
                {
                    continue;
                }

                count++;
                if (firstProv is null && call.Output.TryGetValue("providerName", out var pn) && !string.IsNullOrWhiteSpace(pn))
                {
                    firstProv = pn.Trim();
                }

                if (firstModel is null && call.Output.TryGetValue("modelId", out var mid) && !string.IsNullOrWhiteSpace(mid))
                {
                    firstModel = mid.Trim();
                }

                if (firstPRef is null && call.Output.TryGetValue("promptProfileRef", out var pr) && !string.IsNullOrWhiteSpace(pr))
                {
                    firstPRef = pr.Trim();
                }

                if (firstMRef is null && call.Output.TryGetValue("modelProfileRef", out var mr) && !string.IsNullOrWhiteSpace(mr))
                {
                    firstMRef = mr.Trim();
                }

                if (call.Output.TryGetValue("promptTokens", out var pts)
                    && long.TryParse(pts, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pv))
                {
                    pTok += pv;
                }

                if (call.Output.TryGetValue("completionTokens", out var cts)
                    && long.TryParse(cts, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cv))
                {
                    cTok += cv;
                }

                if (call.Output.TryGetValue("estimatedCostUnits", out var cu)
                    && decimal.TryParse(cu, NumberStyles.Number, CultureInfo.InvariantCulture, out var cd))
                {
                    cost += cd;
                }

                if (call.Output.TryGetValue("latencyMs", out var lm)
                    && long.TryParse(lm, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lv))
                {
                    lat += lv;
                }
            }
        }

        cost = decimal.Round(cost, 9, MidpointRounding.AwayFromZero);

        return new RunManifestModelTelemetry(
            count,
            pTok,
            cTok,
            cost,
            lat,
            firstProv,
            firstModel,
            firstPRef,
            firstMRef);
    }
}