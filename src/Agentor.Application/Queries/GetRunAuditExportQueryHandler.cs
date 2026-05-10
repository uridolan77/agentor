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
using Agentor.Domain;
using Agentor.Domain.Enums;
using Agentor.Domain.Policy;
using Microsoft.Extensions.Options;

namespace Agentor.Application.Queries;

public sealed record RunAuditExportResult(string CanonicalJson, string ContentSha256Hex);

public sealed class GetRunAuditExportQueryHandler
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
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

    public async Task<RunAuditExportResult?> HandleAsync(Guid runId, CancellationToken cancellationToken)
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
        JsonRedaction.Apply(root, RedactionPolicy.FromAuditExportOptions(_options));
        var canonical = root.ToJsonString(SerializerOptions);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        return new RunAuditExportResult(canonical, hash);
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
                run.Trace.OrderBy(t => t.OccurredAt).Select(e => new
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
