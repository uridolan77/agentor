namespace Agentor.Api.Configuration;

/// <summary>
/// Controls OpenAPI document exposure. In Production, OpenAPI is off unless <see cref="Enabled"/> is true.
/// Development hosts enable OpenAPI by default without configuration.
/// </summary>
public sealed class AgentorOpenApiOptions
{
    public const string SectionName = "Agentor:OpenApi";

    /// <summary>
    /// When true, expose <c>/openapi/v1.json</c> even outside Development (e.g. gated staging docs).
    /// </summary>
    public bool Enabled { get; set; }
}
