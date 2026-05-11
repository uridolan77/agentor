namespace Agentor.Infrastructure.Options;

/// <summary>
/// Configuration for <c>scripts/run-integration-smoke.ps1</c> / <c>Agentor.IntegrationSmoke</c> (never commit secrets; use environment variables).
/// </summary>
public sealed class IntegrationSmokeOptions
{
    public const string SectionName = "Agentor:IntegrationSmoke";

    /// <summary>Project id used for Athanor snapshot / canonical / evidence / candidate paths.</summary>
    public Guid AthanorProjectId { get; set; } = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public string AthanorCanonicalLookupKey { get; set; } = "smoke.canonical";

    public string AthanorEvidenceSearchQuery { get; set; } = "smoke-query";

    /// <summary>
    /// When true, runs <see cref="Agentor.Application.Abstractions.IKnowledgeStateClient.SubmitCandidateAsync"/> (POST). Default false — read-only smoke.
    /// </summary>
    public bool AllowAthanorWriteSmoke { get; set; }

    /// <summary>Optional run id for candidate write smoke (defaults to a deterministic GUID).</summary>
    public Guid AthanorCandidateSmokeRunId { get; set; } = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public IntegrationSmokeFamilyOptions Athanor { get; set; } = new();

    public IntegrationSmokeFamilyOptions Conexus { get; set; } = new();

    public IntegrationSmokeFamilyOptions Mcp { get; set; } = new();

    public IntegrationSmokeFamilyOptions ExternalAgents { get; set; } = new();

    /// <summary>HTTP MCP: server id for list-tools + invoke (defaults to demo-server contract shape).</summary>
    public string McpSmokeServerId { get; set; } = "demo-server";

    /// <summary>HTTP MCP: tool name for invoke smoke.</summary>
    public string McpSmokeToolName { get; set; } = "echo";

    /// <summary>External-agent invoke smoke uses this agent key when mode is Fake or Http.</summary>
    public string ExternalAgentSmokeAgentKey { get; set; } = "alpha-agent";

    /// <summary>External-agent invoke smoke capability id.</summary>
    public string ExternalAgentSmokeCapabilityKey { get; set; } = "reply";
}

/// <summary>Per-family smoke mode under <see cref="IntegrationSmokeOptions.SectionName"/>.</summary>
public sealed class IntegrationSmokeFamilyOptions
{
    public SmokeMode Mode { get; set; } = SmokeMode.Disabled;
}
