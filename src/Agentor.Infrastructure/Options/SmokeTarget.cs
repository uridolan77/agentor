namespace Agentor.Infrastructure.Options;

/// <summary>Which integration family smoke steps apply to.</summary>
public enum SmokeTarget
{
    Athanor = 0,
    Conexus = 1,
    Mcp = 2,
    ExternalAgents = 3,
}
