using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Agentor.Application.Abstractions;
using Agentor.Application.Manifest;
using Agentor.Application.Options;
using Agentor.Application.Quality;
using Agentor.Application.Redaction;
using Agentor.Contracts;
using Agentor.Domain;
using Agentor.Domain.Enums;
using Agentor.Domain.Policy;
using Microsoft.Extensions.Options;

namespace Agentor.Application.Queries;

/// <param name="ResponseBody">Payload for the HTTP response (may be pretty JSON or sidecar document).</param>
/// <param name="ContentSha256Hex">SHA-256 of UTF-8 <see cref="CanonicalJson"/> (minified canonical audit).</param>
/// <param name="CanonicalJson">Redacted canonical minified audit JSON used for hashing.</param>
public sealed record RunAuditExportResult(string ResponseBody, string ContentSha256Hex, string CanonicalJson);

public sealed class GetRunAuditExportQueryHandler
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions PrettySerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IAgentRunRepository _repository;
    private readonly AuditExportOptions _options;
    private readonly IPolicyProfileRepository? _policyProfileRepository;

    // 2-param constructor preserves existing test compatibility (no policy repo).
    public GetRunAuditExportQueryHandler(
        IAgentRunRepository repository,
        IOptions<AuditExportOptions> options)
        : this(repository, options, null)
    {
    }

    // Full constructor used by DI when the policy profile repo is registered.
    public GetRunAuditExportQueryHandler(
        IAgentRunRepository repository,
        IOptions<AuditExportOptions> options,
        IPolicyProfileRepository? policyProfileRepository)
    {
        _repository = repository;
        _options = options.Value;
        _policyProfileRepository = policyProfileRepository;
    }

    public Task<RunAuditExportResult?> HandleAsync(Guid runId, CancellationToken cancellationToken) =>
        HandleAsync(runId, AuditExportFormatKind.Canonical, cancellationToken);

    public async Task<RunAuditExportResult?> HandleAsync(
        Guid runId,
        AuditExportFormatKind format,
        CancellationToken cancellationToken)
    {
        var run = await _repository.GetAsync(runId, cancellationToken);
        if (run is null)
        {
            return null;
        }

        ActivePolicyProfile? activePolicyProfile = null;
        if (_policyProfileRepository is not null)
        {
            activePolicyProfile = await _policyProfileRepository.GetActiveAsync(cancellationToken);
        }

        var manifest = RunManifest.FromRun(
            run,
            ModelCallTelemetryAggregator.Aggregate(run),
            ExternalAgentTelemetryAggregator.Aggregate(run));

        var quality = RunQualityGateEvaluator.Evaluate(run, requireCompleted: false);

        var root = BuildAuditRoot(run, manifest, quality, activePolicyProfile);
        var policy = RedactionPolicy.FromAuditExportOptions(_options);

        var clone = JsonNode.Parse(root.ToJsonString(SerializerOptions))
                    ?? throw new InvalidOperationException("Audit root could not be cloned for redaction.");

        var redaction = JsonRedaction.Apply(clone, policy);
        var canonicalJson = clone.ToJsonString(SerializerOptions);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson)));

        var body = format switch
        {
            AuditExportFormatKind.Canonical => canonicalJson,
            AuditExportFormatKind.Pretty => JsonSerializer.Serialize(JsonNode.Parse(canonicalJson), PrettySerializerOptions),
            AuditExportFormatKind.RedactionReport => BuildRedactionReportBody(run.Id, hash, redaction),
            AuditExportFormatKind.HashOnly => BuildHashOnlyBody(run.Id, hash),
            _ => canonicalJson
        };

        return new RunAuditExportResult(body, hash, canonicalJson);
    }

    private static string BuildHashOnlyBody(Guid runId, string hash)
    {
        var o = new JsonObject
        {
            ["schemaVersion"] = "agentor.audit.hashOnly.v1",
            ["runId"] = runId.ToString("D"),
            ["contentSha256Hex"] = hash
        };
        return o.ToJsonString(SerializerOptions);
    }

    private static string BuildRedactionReportBody(Guid runId, string hash, RedactionResult redaction)
    {
        var o = new JsonObject
        {
            ["schemaVersion"] = "agentor.audit.redactionReport.v1",
            ["runId"] = runId.ToString("D"),
            ["contentSha256Hex"] = hash,
            ["redactedPropertyCount"] = redaction.RedactedPropertyCount,
            ["redactedKeyPaths"] = JsonSerializer.SerializeToNode(redaction.RedactedKeyPaths.ToList(), SerializerOptions)
        };
        return o.ToJsonString(PrettySerializerOptions);
    }

    private static JsonObject BuildAuditRoot(
        AgentRun run,
        RunManifest manifest,
        RunQualitySummary quality,
        ActivePolicyProfile? activePolicyProfile = null)
    {
        var root = new JsonObject
        {
            ["schemaVersion"] = "agentor.audit.v1",
            ["run"] = new JsonObject
            {
                ["id"] = run.Id.ToString("D"),
                ["profileId"] = run.ProfileId.ToString("D"),
                ["tenantId"] = run.TenantId?.ToString("D"),
                ["workspaceId"] = run.WorkspaceId?.ToString("D"),
                ["explicitProjectId"] = run.ProjectId?.ToString("D"),
                ["athanorProjectId"] = run.ResolveAthanorProjectId().ToString("D"),
                ["knowledgeScopeId"] = run.KnowledgeScopeId?.ToString("D"),
                ["agentName"] = run.AgentName,
                ["objective"] = run.Objective,
                ["traceId"] = run.TraceId,
                ["status"] = run.Status.ToString(),
                ["startedAt"] = run.StartedAt.ToString("O"),
                ["completedAt"] = run.CompletedAt?.ToString("O"),
                ["errorMessage"] = run.ErrorMessage
            },
            ["plan"] = null,
            ["steps"] = JsonSerializer.SerializeToNode(
                run.Steps.OrderBy(s => s.Index).Select(s => new
                {
                    s.Id,
                    s.Index,
                    s.Name,
                    status = s.Status.ToString(),
                    startedAt = s.StartedAt.ToString("O"),
                    completedAt = s.CompletedAt?.ToString("O"),
                    policyDecisions = s.PolicyDecisions.Select(p => new
                    {
                        p.Id,
                        outcome = p.Outcome.ToString(),
                        p.ReasonCode,
                        p.Reason,
                        decidedAt = p.DecidedAt.ToString("O")
                    }).ToList(),
                    toolCalls = s.ToolCalls.Select(t => new
                    {
                        t.Id,
                        t.ToolKey,
                        status = t.Status.ToString(),
                        input = t.Input,
                        output = t.Output,
                        startedAt = t.StartedAt.ToString("O"),
                        completedAt = t.CompletedAt?.ToString("O"),
                        t.ErrorMessage
                    }).ToList()
                }),
                SerializerOptions),
            ["trace"] = JsonSerializer.SerializeToNode(
                run.Trace.OrderBy(t => t.OccurredAt).ThenBy(t => t.Id).Select(e => new
                {
                    e.Id,
                    kind = e.Kind.ToString(),
                    e.Message,
                    occurredAt = e.OccurredAt.ToString("O"),
                    data = e.Data is null
                        ? null
                        : new SortedDictionary<string, string>(new Dictionary<string, string>(e.Data), StringComparer.Ordinal)
                }),
                SerializerOptions),
            ["humanReviewDecisions"] = JsonSerializer.SerializeToNode(
                run.HumanReviewDecisions.Select(d => new
                {
                    d.Id,
                    kind = d.Kind.ToString(),
                    actorId = d.ActorId.ToString("D"),
                    decidedAt = d.DecidedAt.ToString("O"),
                    d.Note,
                    resolution = d.Resolution.ToString()
                }),
                SerializerOptions),
            ["athanorProvenance"] = JsonSerializer.SerializeToNode(
                run.Trace
                    .Where(e => e.Kind is TraceEventKind.AthanorEvidenceSearchProvenanceAttached
                        or TraceEventKind.AthanorCandidateSubmitted
                        or TraceEventKind.AthanorReviewQueued)
                    .Select(e => new { e.Id, kind = e.Kind.ToString(), occurredAt = e.OccurredAt.ToString("O"), e.Data }),
                SerializerOptions),
            ["conexusTelemetry"] = new JsonObject
            {
                ["modelCallCount"] = manifest.ModelCallCount,
                ["totalModelPromptTokens"] = manifest.TotalModelPromptTokens,
                ["totalModelCompletionTokens"] = manifest.TotalModelCompletionTokens,
                ["totalModelEstimatedCostUnits"] = manifest.TotalModelEstimatedCostUnits,
                ["totalModelLatencyMs"] = manifest.TotalModelLatencyMs,
                ["primaryModelProviderName"] = manifest.PrimaryModelProviderName,
                ["primaryModelId"] = manifest.PrimaryModelId
            },
            ["mcpCalls"] = new JsonArray(),
            ["externalAgentCalls"] = JsonSerializer.SerializeToNode(
                run.Trace.Where(e => e.Kind.ToString().StartsWith("ExternalAgent", StringComparison.Ordinal))
                    .Select(e => new { e.Id, kind = e.Kind.ToString(), occurredAt = e.OccurredAt.ToString("O"), e.Data }),
                SerializerOptions),
            ["sessionMemory"] = JsonSerializer.SerializeToNode(
                new SortedDictionary<string, string>(new Dictionary<string, string>(run.SessionMemory), StringComparer.Ordinal),
                SerializerOptions),
            ["manifestHash"] = manifest.ContentHash,
            ["manifestVersion"] = manifest.ManifestVersion,
            ["qualitySummary"] = JsonSerializer.SerializeToNode(
                new { quality.Passed, quality.Violations, quality.Warnings },
                SerializerOptions),
            ["policyIdentity"] = activePolicyProfile is null ? null : new JsonObject
            {
                ["profileId"] = activePolicyProfile.ProfileId.ToString("D"),
                ["profileName"] = activePolicyProfile.ProfileName,
                ["bundleId"] = activePolicyProfile.BundleId.ToString("D"),
                ["bundleVersion"] = activePolicyProfile.BundleVersion.ToString(),
                ["activatedAt"] = activePolicyProfile.ActivatedAt.ToString("O"),
                ["activatedBy"] = activePolicyProfile.ActivatedBy.ToString("D")
            }
        };

        return root;
    }
}
