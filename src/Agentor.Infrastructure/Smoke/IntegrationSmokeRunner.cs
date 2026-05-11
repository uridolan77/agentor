using System.Globalization;
using System.Net;
using Agentor.Application.Abstractions;
using Agentor.Contracts.Conexus;
using Agentor.Contracts.ExternalAgents;
using Agentor.Contracts.KnowledgeState;
using Agentor.Domain;
using Agentor.Infrastructure.Http;
using Agentor.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Agentor.Infrastructure.Smoke;

public sealed class IntegrationSmokeRunner(
    IOptions<IntegrationSmokeOptions> smokeOptions,
    IKnowledgeStateClient knowledge,
    IModelGatewayClient models,
    IMcpRegistryClient mcp,
    IExternalAgentProtocolClient external)
{
    public async Task<IntegrationSmokeReport> RunAsync(IReadOnlySet<string>? onlyTargets, CancellationToken cancellationToken)
    {
        var smoke = smokeOptions.Value;
        var report = new IntegrationSmokeReport
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Steps = [],
        };

        if (ShouldRun(nameof(SmokeTarget.Athanor), onlyTargets) && smoke.Athanor.Mode != SmokeMode.Disabled)
        {
            await RunAthanorAsync(smoke, report.Steps, cancellationToken).ConfigureAwait(false);
        }

        if (ShouldRun(nameof(SmokeTarget.Conexus), onlyTargets) && smoke.Conexus.Mode != SmokeMode.Disabled)
        {
            await RunConexusAsync(report.Steps, cancellationToken).ConfigureAwait(false);
        }

        if (ShouldRun(nameof(SmokeTarget.Mcp), onlyTargets) && smoke.Mcp.Mode != SmokeMode.Disabled)
        {
            await RunMcpAsync(smoke, report.Steps, cancellationToken).ConfigureAwait(false);
        }

        if (ShouldRun(nameof(SmokeTarget.ExternalAgents), onlyTargets) && smoke.ExternalAgents.Mode != SmokeMode.Disabled)
        {
            await RunExternalAgentsAsync(smoke, report.Steps, cancellationToken).ConfigureAwait(false);
        }

        report.OverallOk = report.Steps.Count == 0 || report.Steps.TrueForAll(s => s.Ok);
        return report;
    }

    private static bool ShouldRun(string target, IReadOnlySet<string>? onlyTargets) =>
        onlyTargets is null || onlyTargets.Contains(target, StringComparer.OrdinalIgnoreCase);

    private async Task RunAthanorAsync(
        IntegrationSmokeOptions smoke,
        List<SmokeStepRecord> steps,
        CancellationToken cancellationToken)
    {
        const string t = nameof(SmokeTarget.Athanor);
        try
        {
            var snap = await knowledge.GetLatestSnapshotAsync(smoke.AthanorProjectId, cancellationToken).ConfigureAwait(false);
            steps.Add(new SmokeStepRecord { Target = t, Name = "latestSnapshot", Ok = true, Detail = snap is null ? "null" : $"snapshotId={snap.SnapshotId}" });
        }
        catch (Exception ex)
        {
            steps.Add(Fail(t, "latestSnapshot", ex));
        }

        try
        {
            var entry = await knowledge.LookupCanonicalEntryAsync(
                    smoke.AthanorProjectId,
                    smoke.AthanorCanonicalLookupKey,
                    cancellationToken)
                .ConfigureAwait(false);
            steps.Add(new SmokeStepRecord { Target = t, Name = "canonicalLookup", Ok = true, Detail = entry is null ? "null" : $"key={entry.Key}" });
        }
        catch (Exception ex)
        {
            steps.Add(Fail(t, "canonicalLookup", ex));
        }

        try
        {
            var hits = await knowledge.SearchEvidenceAsync(smoke.AthanorProjectId, smoke.AthanorEvidenceSearchQuery, cancellationToken).ConfigureAwait(false);
            steps.Add(new SmokeStepRecord { Target = t, Name = "evidenceSearch", Ok = true, Detail = $"count={hits.Count}" });
        }
        catch (Exception ex)
        {
            steps.Add(Fail(t, "evidenceSearch", ex));
        }

        if (smoke.AllowAthanorWriteSmoke)
        {
            try
            {
                var submission = new CandidateKnowledgeSubmissionDto("integration-smoke", "{\"source\":\"Agentor.IntegrationSmoke\"}");
                var submitted = await knowledge.SubmitCandidateAsync(
                        smoke.AthanorProjectId,
                        smoke.AthanorCandidateSmokeRunId,
                        submission,
                        cancellationToken)
                    .ConfigureAwait(false);
                steps.Add(new SmokeStepRecord
                {
                    Target = t,
                    Name = "candidateSubmit",
                    Ok = true,
                    Detail = $"candidateId={submitted.CandidateId};status={submitted.Status}",
                });
            }
            catch (Exception ex)
            {
                steps.Add(Fail(t, "candidateSubmit", ex));
            }
        }
        else
        {
            steps.Add(new SmokeStepRecord
            {
                Target = t,
                Name = "candidateSubmit",
                Ok = true,
                Detail = "skipped (AllowAthanorWriteSmoke=false)",
            });
        }
    }

    private async Task RunConexusAsync(List<SmokeStepRecord> steps, CancellationToken cancellationToken)
    {
        const string t = nameof(SmokeTarget.Conexus);
        try
        {
            var request = ModelCallRequestDto.FromLegacy(
                "integration-smoke",
                "smoke-model",
                promptProfileRef: "pp-smoke",
                modelProfileRef: "mp-smoke",
                declaredCostUnits: 1.23m,
                declaredLatencyMs: 50);

            var result = await models.CompleteAsync(request, cancellationToken).ConfigureAwait(false);
            var flat = result.Payload.ToPolicyEvaluationDictionary();
            var hasTelemetry =
                flat.ContainsKey("promptTokens")
                && flat.ContainsKey("completionTokens")
                && flat.ContainsKey("latencyMs")
                && flat.ContainsKey("estimatedCostUnits");

            var reqFlat = request.Payload.ToPolicyEvaluationDictionary();
            var hasDeclared =
                reqFlat.TryGetValue("declaredCostUnits", out var dcStr)
                && decimal.TryParse(dcStr, CultureInfo.InvariantCulture, out var dcu)
                && dcu == 1.23m
                && reqFlat.TryGetValue("declaredLatencyMs", out var dlStr)
                && int.TryParse(dlStr, CultureInfo.InvariantCulture, out var dli)
                && dli == 50;

            steps.Add(new SmokeStepRecord
            {
                Target = t,
                Name = "modelComplete",
                Ok = hasTelemetry && hasDeclared,
                Detail = hasTelemetry && hasDeclared
                    ? "telemetry+declaredBudgetPresent"
                    : $"telemetry={hasTelemetry};declaredBudget={hasDeclared}",
            });
        }
        catch (Exception ex)
        {
            steps.Add(Fail(t, "modelComplete", ex));
        }
    }

    private async Task RunMcpAsync(IntegrationSmokeOptions smoke, List<SmokeStepRecord> steps, CancellationToken cancellationToken)
    {
        const string t = nameof(SmokeTarget.Mcp);
        try
        {
            var servers = await mcp.ListServersAsync(cancellationToken).ConfigureAwait(false);
            steps.Add(new SmokeStepRecord { Target = t, Name = "listServers", Ok = servers.Count > 0, Detail = $"count={servers.Count}" });
        }
        catch (Exception ex)
        {
            steps.Add(Fail(t, "listServers", ex));
        }

        try
        {
            var tools = await mcp.ListToolsAsync(smoke.McpSmokeServerId, cancellationToken).ConfigureAwait(false);
            steps.Add(new SmokeStepRecord { Target = t, Name = "listTools", Ok = tools.Count > 0, Detail = $"count={tools.Count}" });
        }
        catch (Exception ex)
        {
            steps.Add(Fail(t, "listTools", ex));
        }

        try
        {
            var args = ToolPayload.FromLegacyDictionary(
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["text"] = "integration-smoke" });
            var invoked = await mcp.InvokeToolAsync(smoke.McpSmokeServerId, smoke.McpSmokeToolName, args, cancellationToken).ConfigureAwait(false);
            steps.Add(new SmokeStepRecord
            {
                Target = t,
                Name = "invokeTool",
                Ok = invoked.Success,
                Detail = invoked.Success ? "success" : IntegrationFailureRedaction.RedactAndTruncate(invoked.ErrorMessage),
            });
        }
        catch (Exception ex)
        {
            steps.Add(Fail(t, "invokeTool", ex));
        }
    }

    private async Task RunExternalAgentsAsync(IntegrationSmokeOptions smoke, List<SmokeStepRecord> steps, CancellationToken cancellationToken)
    {
        const string t = nameof(SmokeTarget.ExternalAgents);
        try
        {
            var caps = await external.ListCapabilitiesAsync(ExternalAgentProtocolKind.A2AStyled, cancellationToken).ConfigureAwait(false);
            steps.Add(new SmokeStepRecord { Target = t, Name = "discover", Ok = caps.Count > 0, Detail = $"count={caps.Count}" });
        }
        catch (Exception ex)
        {
            steps.Add(Fail(t, "discover", ex));
        }

        try
        {
            var req = new ExternalAgentInvocationRequestDto(
                ExternalAgentProtocolKind.A2AStyled,
                smoke.ExternalAgentSmokeAgentKey,
                smoke.ExternalAgentSmokeCapabilityKey,
                ToolPayload.FromLegacyDictionary(
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["ping"] = "smoke" }));

            var result = await external.InvokeAsync(req, cancellationToken).ConfigureAwait(false);
            steps.Add(new SmokeStepRecord
            {
                Target = t,
                Name = "invoke",
                Ok = result.Status == ExternalAgentInvocationStatus.Succeeded,
                Detail = result.Status.ToString(),
            });
        }
        catch (Exception ex)
        {
            steps.Add(Fail(t, "invoke", ex));
        }
    }

    private static SmokeStepRecord Fail(string target, string name, Exception ex)
    {
        var http = ex as HttpRequestException;
        return new SmokeStepRecord
        {
            Target = target,
            Name = name,
            Ok = false,
            HttpStatus = http?.StatusCode is HttpStatusCode code ? (int)code : null,
            Detail = IntegrationFailureRedaction.RedactAndTruncate(ex.Message),
        };
    }
}
