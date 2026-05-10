namespace Agentor.Application.Options;

/// <summary>
/// Controls how <c>POST /api/v1/agent-runs</c> interprets requests without explicit execution selectors.
/// </summary>
public sealed class AgentorPublicRunOptions
{
    public const string SectionName = "Agentor:PublicRuns";

    /// <summary>
    /// When true, a request with no <c>planId</c>, <c>toolKey</c>, <c>skillKey</c>, <c>recipeId</c>, and no explicit non-legacy <c>mode</c>
    /// is treated as LegacyFakeTool (backward compatible with early harness defaults).
    /// Set false in production to require explicit routing.
    /// </summary>
    public bool TreatMissingExecutionSelectorAsLegacyFakeTool { get; set; } = true;
}
