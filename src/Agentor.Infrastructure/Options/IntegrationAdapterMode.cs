namespace Agentor.Infrastructure.Options;

/// <summary>
/// Agentor integration wiring mode for external services (Infrastructure adapters only).
/// </summary>
public enum IntegrationAdapterMode
{
    Fake = 0,
    Http = 1,
    Disabled = 2,
}
