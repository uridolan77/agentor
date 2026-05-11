using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Agentor.Api.Security;
using Agentor.Application.Abstractions;
using Agentor.Application.Options;
using Agentor.Application.Reliability;
using Agentor.Application.RunQueue;
using Agentor.Contracts;
using Agentor.Domain.Enums;
using Agentor.Domain.Policy;
using Agentor.Infrastructure.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Agentor.Api.Diagnostics;

/// <summary>
/// Operator-safe diagnostics bundle (JSON + Markdown). No secrets, payloads, stack traces, or connection strings.
/// </summary>
public sealed class OperatorDiagnosticsService(
    IAgentRunRepository runs,
    IDurableRunQueue queue,
    IOutboxStore outbox,
    IIntegrationStatusReader integration,
    IPolicyProfileRepository profiles,
    IPolicyBundleRepository bundles,
    IOptionsMonitor<AgentorRuntimeOptions> runtime,
    IOptionsMonitor<AgentorPersistenceOptions> persistence,
    IOptionsMonitor<AgentorAuthOptions> auth,
    IOptionsMonitor<RunWorkerOptions> worker,
    IOptionsMonitor<OutboxDispatchOptions> outboxDispatch,
    IOptionsMonitor<RunQueueOptions> runQueue,
    IHostEnvironment environment)
{
    public async Task<(string Json, string Markdown)> BuildAsync(bool openApiDocumentEnabled, CancellationToken cancellationToken)
    {
        var failedPage = await runs.ListSummariesAsync(0, 1, cancellationToken, AgentRunStatus.Failed).ConfigureAwait(false);
        var reviewPage = await runs.ListSummariesAsync(0, 1, cancellationToken, AgentRunStatus.RequiresReview).ConfigureAwait(false);
        var queueRows = await queue.ListLatestAsync(2000, cancellationToken).ConfigureAwait(false);
        var outboxRows = await outbox.ListLatestAsync(2000, cancellationToken).ConfigureAwait(false);
        var integrationStatus = await integration.GetStatusAsync(cancellationToken).ConfigureAwait(false);
        var active = await profiles.GetActiveAsync(cancellationToken).ConfigureAwait(false);
        PolicyBundle? bundle = null;
        if (active is not null)
        {
            bundle = await bundles.GetAsync(active.BundleId, cancellationToken).ConfigureAwait(false);
        }

        var queueDepth = queueRows.Count(static r =>
            r.Status is DurableRunQueueStatus.Pending or DurableRunQueueStatus.Claimed);
        var outboxPending = outboxRows.Count(static r =>
            r.Status is OutboxStatus.Pending or OutboxStatus.Dispatching);

        var root = new JsonObject
        {
            ["schema"] = "agentor.diagnostics.v1",
            ["generatedAtUtc"] = DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            ["runtime"] = new JsonObject
            {
                ["serviceName"] = runtime.CurrentValue.ServiceName,
                ["version"] = runtime.CurrentValue.Version,
                ["environment"] = environment.EnvironmentName,
            },
            ["auth"] = new JsonObject
            {
                ["mode"] = auth.CurrentValue.Mode.ToString(),
                ["jwtAcceptUnvalidatedBearerTokens"] = auth.CurrentValue.JwtAcceptUnvalidatedBearerTokens,
                ["jwtAllowUnvalidatedTokensOutsideDevelopment"] = auth.CurrentValue.JwtAllowUnvalidatedTokensOutsideDevelopment,
            },
            ["openApi"] = new JsonObject
            {
                ["documentEnabled"] = openApiDocumentEnabled,
            },
            ["persistence"] = new JsonObject
            {
                ["mode"] = persistence.CurrentValue.Mode,
                ["connectionStringConfigured"] = !string.IsNullOrWhiteSpace(persistence.CurrentValue.ConnectionString),
            },
            ["workers"] = new JsonObject
            {
                ["runQueueExecutionMode"] = runQueue.CurrentValue.ExecutionMode.ToString(),
                ["runWorkerEnabled"] = worker.CurrentValue.Enabled,
                ["outboxDispatchEnabled"] = outboxDispatch.CurrentValue.Enabled,
            },
            ["runs"] = new JsonObject
            {
                ["recentFailedRunsTotal"] = failedPage.TotalCount,
                ["reviewBacklogTotal"] = reviewPage.TotalCount,
            },
            ["queue"] = new JsonObject
            {
                ["approxDepth"] = queueDepth,
            },
            ["outbox"] = new JsonObject
            {
                ["approxPending"] = outboxPending,
            },
            ["policy"] = BuildPolicyJson(active, bundle),
            ["integrations"] = BuildIntegrationsJson(integrationStatus),
            ["evaluationArtifacts"] = new JsonObject
            {
                ["present"] = false,
                ["note"] = "Runtime API does not scan on-disk evaluation harness fixtures.",
            },
        };

        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        var json = root.ToJsonString(jsonOptions);
        var md = BuildMarkdown(
            json,
            environment.EnvironmentName,
            runtime.CurrentValue.Version,
            auth.CurrentValue.Mode.ToString(),
            openApiDocumentEnabled,
            persistence.CurrentValue.Mode,
            failedPage.TotalCount,
            reviewPage.TotalCount,
            queueDepth,
            outboxPending);
        return (json, md);
    }

    private static JsonObject BuildPolicyJson(ActivePolicyProfile? active, PolicyBundle? bundle)
    {
        var o = new JsonObject
        {
            ["activeProfileConfigured"] = active is not null,
        };

        if (active is null)
        {
            return o;
        }

        o["profileId"] = active.ProfileId.ToString("D", CultureInfo.InvariantCulture);
        o["profileName"] = active.ProfileName;
        o["bundleId"] = active.BundleId.ToString("D", CultureInfo.InvariantCulture);
        o["bundleVersion"] = $"{active.BundleVersion.Major}.{active.BundleVersion.Minor}";
        o["bundlePublished"] = bundle is not null;
        return o;
    }

    private static JsonObject BuildIntegrationsJson(IntegrationsStatusResponseDto status)
    {
        var o = new JsonObject();
        foreach (var name in status.Integrations.Keys.Order(StringComparer.OrdinalIgnoreCase))
        {
            var a = status.Integrations[name];
            o[name] = new JsonObject
            {
                ["mode"] = a.Mode,
                ["ready"] = a.Ready,
                ["detail"] = a.Detail is null ? null : JsonValue.Create(a.Detail),
            };
        }

        return o;
    }

    private static string BuildMarkdown(
        string embeddedJson,
        string environmentName,
        string version,
        string authMode,
        bool openApiEnabled,
        string persistenceMode,
        int failedTotal,
        int reviewTotal,
        int queueDepth,
        int outboxPending)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Agentor diagnostics report");
        sb.AppendLine();
        sb.AppendLine("This bundle is intentionally redacted: no tool payloads, audit bodies, tokens, connection strings, or stack traces.");
        sb.AppendLine();
        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.Append("- **Environment**: ").AppendLine(environmentName);
        sb.Append("- **Service version**: ").AppendLine(version);
        sb.Append("- **Auth mode**: ").AppendLine(authMode);
        sb.Append("- **OpenAPI document enabled**: ").AppendLine(openApiEnabled ? "true" : "false");
        sb.Append("- **Persistence mode**: ").AppendLine(persistenceMode);
        sb.Append("- **Approx queue depth**: ").AppendLine(queueDepth.ToString(CultureInfo.InvariantCulture));
        sb.Append("- **Approx outbox pending**: ").AppendLine(outboxPending.ToString(CultureInfo.InvariantCulture));
        sb.Append("- **Failed runs (total)**: ").AppendLine(failedTotal.ToString(CultureInfo.InvariantCulture));
        sb.Append("- **Requires-review runs (total)**: ").AppendLine(reviewTotal.ToString(CultureInfo.InvariantCulture));
        sb.AppendLine();
        sb.AppendLine("## Proof boundaries");
        sb.AppendLine();
        sb.AppendLine("- Counts are repository snapshots at generation time.");
        sb.AppendLine("- Integration rows mirror `/api/v1/integrations/status` without upstream bodies.");
        sb.AppendLine("- Policy section reflects active profile metadata only (no rule bodies).");
        sb.AppendLine();
        sb.AppendLine("## JSON");
        sb.AppendLine();
        sb.AppendLine("```json");
        sb.AppendLine(embeddedJson);
        sb.AppendLine("```");
        return sb.ToString();
    }
}
